using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Options;
using Twino.MQ.Queues.States;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues
{
    /// <summary>
    /// Channel queue.
    /// Keeps queued messages and subscribed clients.
    /// </summary>
    public class ChannelQueue
    {
        #region Properties

        /// <summary>
        /// Channel of the queue
        /// </summary>
        public Channel Channel { get; }

        /// <summary>
        /// Queue status
        /// </summary>
        public QueueStatus Status { get; private set; }

        /// <summary>
        /// Current status state object
        /// </summary>
        internal IQueueState State { get; private set; }

        /// <summary>
        /// Queue content type
        /// </summary>
        public ushort Id { get; }

        /// <summary>
        /// Queue options.
        /// If null, channel default options will be used
        /// </summary>
        public ChannelQueueOptions Options { get; }

        /// <summary>
        /// Queue messaging handler.
        /// If null, server's default delivery will be used.
        /// </summary>
        public IMessageDeliveryHandler DeliveryHandler { get; }

        /// <summary>
        /// Queue statistics and information
        /// </summary>
        public QueueInfo Info { get; } = new QueueInfo();

        /// <summary>
        /// High priority message list
        /// </summary>
        public IEnumerable<QueueMessage> HighPriorityMessages => HighPriorityLinkedList;

        /// <summary>
        /// High priority message list
        /// </summary>
        internal readonly LinkedList<QueueMessage> HighPriorityLinkedList = new LinkedList<QueueMessage>();

        /// <summary>
        /// Standard priority queue message
        /// </summary>
        public IEnumerable<QueueMessage> RegularMessages => RegularLinkedList;

        /// <summary>
        /// Standard priority queue message
        /// </summary>
        internal readonly LinkedList<QueueMessage> RegularLinkedList = new LinkedList<QueueMessage>();

        /// <summary>
        /// Time keeper for the queue.
        /// Checks message receiver deadlines and delivery deadlines.
        /// </summary>
        internal QueueTimeKeeper TimeKeeper { get; }

        /// <summary>
        /// Wait acknowledge cross thread locker
        /// </summary>
        private SemaphoreSlim _semaphore;

        /// <summary>
        /// This task holds the code until acknowledge is received
        /// </summary>
        private TaskCompletionSource<bool> _acknowledgeCallback;

        /// <summary>
        /// Trigger locker field.
        /// Used to prevent concurrent trigger method calls.
        /// </summary>
        private volatile bool _triggering;

        #endregion

        #region Constructors - Destroy

        internal ChannelQueue(Channel channel,
                              ushort id,
                              ChannelQueueOptions options,
                              IMessageDeliveryHandler deliveryHandler)
        {
            Channel = channel;
            Id = id;
            Options = options;
            Status = options.Status;
            DeliveryHandler = deliveryHandler;
            State = QueueStateFactory.Create(this, options.Status);

            TimeKeeper = new QueueTimeKeeper(this);
            TimeKeeper.Run();

            if (options.WaitForAcknowledge)
                _semaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Destorys the queue
        /// </summary>
        public async Task Destroy()
        {
            await TimeKeeper.Destroy();

            lock (HighPriorityLinkedList)
                HighPriorityLinkedList.Clear();

            lock (RegularLinkedList)
                RegularLinkedList.Clear();

            if (_semaphore != null)
            {
                _semaphore.Dispose();
                _semaphore = null;
            }
        }

        #endregion

        #region Messages

        /// <summary>
        /// Returns pending high priority messages count
        /// </summary>
        public int HighPriorityMessageCount()
        {
            return HighPriorityLinkedList.Count;
        }

        /// <summary>
        /// Returns pending regular messages count
        /// </summary>
        public int RegularMessageCount()
        {
            return RegularLinkedList.Count;
        }

        /// <summary>
        /// Finds and returns next queue message.
        /// Message will not be removed from the queue.
        /// If there is no message in queue, returns null
        /// </summary>
        public QueueMessage FindNextMessage()
        {
            if (HighPriorityLinkedList.Count > 0)
            {
                lock (HighPriorityLinkedList)
                    return HighPriorityLinkedList.First.Value;
            }

            if (RegularLinkedList.Count > 0)
            {
                lock (RegularLinkedList)
                    return RegularLinkedList.First.Value;
            }

            return null;
        }

        /// <summary>
        /// Clears all messages in queue
        /// </summary>
        public void ClearRegularMessages()
        {
            lock (RegularLinkedList)
                RegularLinkedList.Clear();
        }

        /// <summary>
        /// Clears all messages in queue
        /// </summary>
        public void ClearHighPriorityMessages()
        {
            lock (HighPriorityLinkedList)
                HighPriorityLinkedList.Clear();
        }

        /// <summary>
        /// Clears all messages in queue
        /// </summary>
        public void ClearAllMessages()
        {
            ClearRegularMessages();
            ClearHighPriorityMessages();
        }

        /// <summary>
        /// Changes message priority.
        /// Removes message from previous prio queue and puts it new prio queue.
        /// If putBack is true, item will be put to the end of the queue
        /// </summary>
        public async Task<bool> ChangeMessagePriority(QueueMessage message, bool highPriority, bool putBack = true)
        {
            if (message.Message.HighPriority == highPriority)
                return false;

            await RemoveMessage(message, true, true);
            message.Message.HighPriority = highPriority;

            if (highPriority)
            {
                lock (HighPriorityLinkedList)
                {
                    if (putBack)
                        HighPriorityLinkedList.AddLast(message);
                    else
                        HighPriorityLinkedList.AddFirst(message);
                }

                Info.UpdateHighPriorityMessageCount(HighPriorityLinkedList.Count);
            }
            else
            {
                lock (RegularLinkedList)
                {
                    if (putBack)
                        RegularLinkedList.AddLast(message);
                    else
                        RegularLinkedList.AddFirst(message);
                }

                Info.UpdateRegularMessageCount(RegularLinkedList.Count);
            }

            return true;
        }

        /// <summary>
        /// Removes the message from the queue.
        /// Remove operation will be canceled If force is false and message is not sent.
        /// If silent is false, MessageRemoved method of delivery handler is called
        /// </summary>
        public async Task<bool> RemoveMessage(QueueMessage message, bool force = false, bool silent = false)
        {
            if (!force && !message.IsSent)
                return false;

            if (message.Message.HighPriority)
                HighPriorityLinkedList.Remove(message);
            else
                RegularLinkedList.Remove(message);

            if (!silent)
            {
                Info.AddMessageRemove();
                await DeliveryHandler.MessageRemoved(this, message);
            }

            return true;
        }

        /// <summary>
        /// Adds message into the queue
        /// </summary>
        internal void AddMessage(QueueMessage message, bool toEnd = true)
        {
            if (message.IsInQueue)
                return;

            if (message.Message.HighPriority)
            {
                lock (HighPriorityLinkedList)
                {
                    //re-check when locked.
                    //it's checked before lock, because we dont wanna lock anything in non-concurrent already queued situations
                    if (message.IsInQueue)
                        return;

                    if (toEnd)
                        HighPriorityLinkedList.AddLast(message);
                    else
                        HighPriorityLinkedList.AddFirst(message);

                    message.IsInQueue = true;
                }

                Info.UpdateHighPriorityMessageCount(HighPriorityLinkedList.Count);
            }
            else
            {
                lock (RegularLinkedList)
                {
                    //re-check when locked
                    //it's checked before lock, because we dont wanna lock anything in non-concurrent already queued situations
                    if (message.IsInQueue)
                        return;

                    if (toEnd)
                        RegularLinkedList.AddLast(message);
                    else
                        RegularLinkedList.AddFirst(message);

                    message.IsInQueue = true;
                }

                Info.UpdateRegularMessageCount(RegularLinkedList.Count);
            }
        }

        #endregion

        #region Status Actions

        /// <summary>
        /// Sets status of the queue
        /// </summary>
        public async Task SetStatus(QueueStatus status)
        {
            QueueStatus old = Status;
            if (old == status)
                return;

            if (Channel.EventHandler != null)
            {
                bool allowed = await Channel.EventHandler.OnQueueStatusChanged(this, old, status);
                if (!allowed)
                    return;
            }

            //clear all queue messages if new status is stopped
            if (status == QueueStatus.Stopped)
            {
                lock (HighPriorityLinkedList)
                    HighPriorityLinkedList.Clear();

                lock (RegularLinkedList)
                    RegularLinkedList.Clear();

                TimeKeeper.Reset();
            }

            Status = status;
            State = QueueStateFactory.Create(this, status);

            //trigger queued messages
            if (status == QueueStatus.Route || status == QueueStatus.Push || status == QueueStatus.RoundRobin)
                await Trigger();
        }

        /// <summary>
        /// Stop the queue, clears all queued messages and re-starts
        /// </summary>
        /// <returns></returns>
        public async Task Restart()
        {
            QueueStatus prev = Status;
            await SetStatus(QueueStatus.Stopped);
            await SetStatus(prev);
        }

        #endregion

        #region Delivery

        /// <summary>
        /// Pushes a message into the queue.
        /// </summary>
        internal async Task<PushResult> Push(QueueMessage message, MqClient sender)
        {
            if (Status == QueueStatus.Stopped)
                return PushResult.StatusNotSupported;

            if (Options.MessageLimit > 0 && HighPriorityLinkedList.Count + RegularLinkedList.Count >= Options.MessageLimit)
                return PushResult.LimitExceeded;

            //prepare properties
            message.Message.FirstAcquirer = true;
            message.Message.AcknowledgeRequired = Options.RequestAcknowledge;

            //if message doesn't have message id and "UseMessageId" option is enabled, create new message id for the message
            if (Options.UseMessageId && string.IsNullOrEmpty(message.Message.MessageId))
                message.Message.SetMessageId(Channel.Server.MessageIdGenerator.Create());

            //if we have an option maximum wait duration for message, set it after message joined to the queue.
            //time keeper will check this value and if message time is up, it will remove message from the queue.
            if (Options.MessageTimeout > TimeSpan.Zero)
                message.Deadline = DateTime.UtcNow.Add(Options.MessageTimeout);

            if (Options.HideClientNames)
                message.Message.SetSource(null);

            //process the message
            QueueMessage held = null;
            try
            {
                //fire message receive event
                Info.AddMessageReceive();
                Decision decision = await DeliveryHandler.ReceivedFromProducer(this, message, sender);
                message.Decision = decision;

                bool allow = await ApplyDecision(decision, message);
                if (!allow)
                    return PushResult.Success;

                return await State.Push(message, sender);
            }
            catch (Exception ex)
            {
                Info.AddError();
                try
                {
                    Decision decision = await DeliveryHandler.ExceptionThrown(this, State.ProcessingMessage, ex);
                    if (State.ProcessingMessage != null)
                    {
                        await ApplyDecision(decision, State.ProcessingMessage);

                        if (decision.KeepMessage && !State.ProcessingMessage.IsInQueue)
                            AddMessage(State.ProcessingMessage, false);
                    }
                }
                catch //if developer does wrong operation, we should not stop
                {
                }
            }

            return PushResult.Success;
        }

        /// <summary>
        /// Checks all pending messages and subscribed receivers.
        /// If they should receive the messages, runs the process.
        /// This method is called automatically after a client joined to channel or status has changed.
        /// You can call manual after you filled queue manually.
        /// </summary>
        public async Task Trigger()
        {
            if (Channel.ClientsCount() == 0)
                return;

            if (_triggering)
                return;

            _triggering = true;
            await State.Trigger();
            _triggering = false;
        }

        #endregion

        #region Decision

        /// <summary>
        /// Creates final decision from multiple decisions.
        /// Final decision has bests choices for each decision.
        /// </summary>
        internal static Decision CreateFinalDecision(Decision final, Decision decision)
        {
            bool allow = false;
            bool keep = false;
            bool save = false;
            DeliveryAcknowledgeDecision ack = DeliveryAcknowledgeDecision.None;

            if (decision.Allow)
                allow = true;

            if (decision.KeepMessage)
                keep = true;

            if (decision.SaveMessage)
                save = true;

            if (decision.SendAcknowledge == DeliveryAcknowledgeDecision.Always)
                ack = DeliveryAcknowledgeDecision.Always;

            else if (decision.SendAcknowledge == DeliveryAcknowledgeDecision.IfSaved && final.SendAcknowledge == DeliveryAcknowledgeDecision.None)
                ack = DeliveryAcknowledgeDecision.IfSaved;

            return new Decision(allow, save, keep, ack);
        }

        /// <summary>
        /// Applies decision.
        /// If save is chosen, saves the message.
        /// If acknowledge is chosen, sends an ack message to source.
        /// Returns true is allowed
        /// </summary>
        internal async Task<bool> ApplyDecision(Decision decision, QueueMessage message, TmqMessage customAck = null)
        {
            if (decision.SaveMessage && !message.IsSaved)
            {
                message.IsSaved = await DeliveryHandler.SaveMessage(this, message);

                if (message.IsSaved)
                    Info.AddMessageSave();
            }

            if (decision.SendAcknowledge == DeliveryAcknowledgeDecision.Always ||
                decision.SendAcknowledge == DeliveryAcknowledgeDecision.IfSaved && message.IsSaved)
            {
                if (message.Source != null && message.Source.IsConnected)
                {
                    TmqMessage acknowledge = customAck ?? message.Message.CreateAcknowledge();
                    await message.Source.SendAsync(acknowledge);
                }
            }

            if (decision.KeepMessage)
                AddMessage(message, false);

            else if (!decision.Allow)
            {
                Info.AddMessageRemove();
                _ = DeliveryHandler.MessageRemoved(this, message);
            }

            return decision.Allow;
        }

        #endregion

        #region Acknowledge

        /// <summary>
        /// When wait for acknowledge is active, this method locks the queue until acknowledge is received
        /// </summary>
        internal async Task WaitForAcknowledge(QueueMessage message)
        {
            //if we will lock the queue until ack received, we must request ack
            if (!message.Message.AcknowledgeRequired)
                message.Message.AcknowledgeRequired = true;

            if (_acknowledgeCallback == null)
                return;

            //lock the object, because pending ack message should be queued
            if (_semaphore == null)
                _semaphore = new SemaphoreSlim(1, 1);

            await _semaphore.WaitAsync();
            try
            {
                bool received = await _acknowledgeCallback.Task;
                _acknowledgeCallback = new TaskCompletionSource<bool>();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Called when a acknowledge message is received from the client
        /// </summary>
        internal async Task AcknowledgeDelivered(MqClient from, TmqMessage deliveryMessage)
        {
            MessageDelivery delivery = TimeKeeper.FindDelivery(from, deliveryMessage.MessageId);

            if (delivery != null)
                delivery.MarkAsAcknowledged();

            Info.AddAcknowledge();
            Decision decision = await DeliveryHandler.AcknowledgeReceived(this, deliveryMessage, delivery);

            if (delivery != null)
            {
                if (Options.HideClientNames)
                    deliveryMessage.SetSource(null);

                await ApplyDecision(decision, delivery.Message, deliveryMessage);
            }

            ReleaseAcknowledgeLock(true);
        }

        /// <summary>
        /// If acknowledge lock option is enabled, releases the lock
        /// </summary>
        internal void ReleaseAcknowledgeLock(bool received)
        {
            if (_acknowledgeCallback != null)
            {
                TaskCompletionSource<bool> ack = _acknowledgeCallback;
                _acknowledgeCallback = null;
                ack.SetResult(received);
            }
        }

        #endregion
    }
}