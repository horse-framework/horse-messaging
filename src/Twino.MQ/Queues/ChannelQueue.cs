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
        public IMessageDeliveryHandler DeliveryHandler { get; private set; }

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
        private SemaphoreSlim _ackSync;

        /// <summary>
        /// Sync object for inserting messages into queue as FIFO
        /// </summary>
        private readonly SemaphoreSlim _listSync = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Pushing messages cross thread locker
        /// </summary>
        private readonly SemaphoreSlim _pushSync = new SemaphoreSlim(1, 1);

        /// <summary>
        /// This task holds the code until acknowledge is received
        /// </summary>
        private TaskCompletionSource<bool> _acknowledgeCallback;

        /// <summary>
        /// Trigger locker field.
        /// Used to prevent concurrent trigger method calls.
        /// </summary>
        private volatile bool _triggering;

        /// <summary>
        /// Payload object for end-user usage
        /// </summary>
        public object Payload { get; set; }

        /// <summary>
        /// Checks queue if trigger required triggers.
        /// In usual, this timer should never start to triggers, it's just plan b.
        /// </summary>
        private Timer _triggerTimer;

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
                _ackSync = new SemaphoreSlim(1, 1);

            _triggerTimer = new Timer(a =>
            {
                if (!_triggering && State.TriggerSupported)
                    _ = Trigger();
            }, null, TimeSpan.FromSeconds(5000), TimeSpan.FromSeconds(5000));
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

            if (_ackSync != null)
            {
                _ackSync.Dispose();
                _ackSync = null;
            }

            if (_listSync != null)
                _listSync.Dispose();

            if (_pushSync != null)
                _pushSync.Dispose();

            if (_triggerTimer != null)
            {
                await _triggerTimer.DisposeAsync();
                _triggerTimer = null;
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

            await _listSync.WaitAsync();
            try
            {
                if (highPriority)
                {
                    if (putBack)
                        HighPriorityLinkedList.AddLast(message);
                    else
                        HighPriorityLinkedList.AddFirst(message);

                    Info.UpdateHighPriorityMessageCount(HighPriorityLinkedList.Count);
                }
                else
                {
                    if (putBack)
                        RegularLinkedList.AddLast(message);
                    else
                        RegularLinkedList.AddFirst(message);

                    Info.UpdateRegularMessageCount(RegularLinkedList.Count);
                }
            }
            finally
            {
                _listSync.Release();
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

            await _listSync.WaitAsync();
            try
            {
                if (message.Message.HighPriority)
                    HighPriorityLinkedList.Remove(message);
                else
                    RegularLinkedList.Remove(message);
            }
            finally
            {
                _listSync.Release();
            }

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

        /// <summary>
        /// Returns true, if there are no messages in queue
        /// </summary>
        public bool IsEmpty()
        {
            return HighPriorityLinkedList.Count == 0 && RegularLinkedList.Count == 0;
        }

        #endregion

        #region Status Actions

        /// <summary>
        /// Sets status of the queue
        /// </summary>
        public async Task SetStatus(QueueStatus status, IMessageDeliveryHandler newDeliveryHandler = null)
        {
            QueueStatus prevStatus = Status;
            IQueueState prevState = State;

            if (prevStatus == status)
                return;

            QueueStatusAction leave = await State.LeaveStatus(status);

            if (leave == QueueStatusAction.Deny)
                return;

            if (leave == QueueStatusAction.DenyAndTrigger)
            {
                await Trigger();
                return;
            }

            Status = status;
            State = QueueStateFactory.Create(this, status);

            QueueStatusAction enter = await State.EnterStatus(prevStatus);
            if (enter == QueueStatusAction.Deny || enter == QueueStatusAction.DenyAndTrigger)
            {
                Status = prevStatus;
                State = prevState;
                await prevState.EnterStatus(prevStatus);

                if (enter == QueueStatusAction.DenyAndTrigger)
                    await Trigger();

                return;
            }

            if (Channel.EventHandler != null)
                await Channel.EventHandler.OnQueueStatusChanged(this, prevStatus, status);

            if (enter == QueueStatusAction.AllowAndTrigger)
                _ = Trigger();
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

            if (Options.MessageSizeLimit > 0 && message.Message.Length > Options.MessageSizeLimit)
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

            try
            {
                //fire message receive event
                Info.AddMessageReceive();
                Decision decision = await DeliveryHandler.ReceivedFromProducer(this, message, sender);
                message.Decision = decision;

                bool allow = await ApplyDecision(decision, message);
                if (!allow)
                    return PushResult.Success;

                if (State.CanEnqueue(message))
                {
                    await RunInListSync(() => AddMessage(message));

                    if (State.TriggerSupported && !_triggering)
                        _ = Trigger();
                }
                else
                    _ = State.Push(message);

                return PushResult.Success;
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

        internal async Task RunInListSync(Action action)
        {
            await _listSync.WaitAsync();
            try
            {
                action();
            }
            finally
            {
                _listSync.Release();
            }
        }

        /// <summary>
        /// Checks all pending messages and subscribed receivers.
        /// If they should receive the messages, runs the process.
        /// This method is called automatically after a client joined to channel or status has changed.
        /// You can call manual after you filled queue manually.
        /// </summary>
        public async Task Trigger()
        {
            if (_triggering)
                return;

            await _pushSync.WaitAsync();
            try
            {
                if (_triggering || !State.TriggerSupported)
                    return;

                if (Channel.ClientsCount() == 0)
                    return;

                _triggering = true;

                if (HighPriorityLinkedList.Count > 0)
                    await ProcessPendingMessages(true);

                if (RegularLinkedList.Count > 0)
                    await ProcessPendingMessages(false);
            }
            finally
            {
                _triggering = false;
                _pushSync.Release();
            }
        }


        /// <summary>
        /// Start to process all pending messages.
        /// This method is called after a client is subscribed to the queue.
        /// </summary>
        private async Task ProcessPendingMessages(bool high)
        {
            while (State.TriggerSupported)
            {
                QueueMessage message;

                if (high)
                {
                    lock (HighPriorityLinkedList)
                    {
                        if (HighPriorityLinkedList.Count == 0)
                            return;

                        message = HighPriorityLinkedList.First.Value;
                        HighPriorityLinkedList.RemoveFirst();
                        message.IsInQueue = false;
                    }
                }
                else
                {
                    lock (RegularLinkedList)
                    {
                        if (RegularLinkedList.Count == 0)
                            return;

                        message = RegularLinkedList.First.Value;
                        RegularLinkedList.RemoveFirst();
                        message.IsInQueue = false;
                    }
                }

                try
                {
                    PushResult pr = await State.Push(message);
                    if (pr == PushResult.Empty || pr == PushResult.NoConsumers)
                        return;
                }
                catch (Exception ex)
                {
                    Info.AddError();
                    try
                    {
                        Decision decision = await DeliveryHandler.ExceptionThrown(this, message, ex);
                        await ApplyDecision(decision, message);
                    }
                    catch //if developer does wrong operation, we should not stop
                    {
                    }
                }
            }
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
            if (decision.SaveMessage)
                await SaveMessage(message);

            if (decision.SendAcknowledge == DeliveryAcknowledgeDecision.Always ||
                decision.SendAcknowledge == DeliveryAcknowledgeDecision.IfSaved && message.IsSaved)
            {
                TmqMessage acknowledge = customAck ?? message.Message.CreateAcknowledge();
                if (message.Source != null && message.Source.IsConnected)
                {
                    bool sent = await message.Source.SendAsync(acknowledge);
                    if (decision.AcknowledgeDelivery != null)
                        await decision.AcknowledgeDelivery(message, message.Source, sent);
                }
                else if (decision.AcknowledgeDelivery != null)
                    await decision.AcknowledgeDelivery(message, message.Source, false);
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

        /// <summary>
        /// Saves message
        /// </summary>
        public async Task<bool> SaveMessage(QueueMessage message)
        {
            if (message.IsSaved)
                return false;
            
            message.IsSaved = await DeliveryHandler.SaveMessage(this, message);

            if (message.IsSaved)
                Info.AddMessageSave();

            return message.IsSaved;
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
            if (_ackSync == null)
                _ackSync = new SemaphoreSlim(1, 1);

            await _ackSync.WaitAsync();
            try
            {
                bool received = await _acknowledgeCallback.Task;
                _acknowledgeCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            finally
            {
                _ackSync.Release();
            }
        }

        /// <summary>
        /// Called when a acknowledge message is received from the client
        /// </summary>
        internal async Task AcknowledgeDelivered(MqClient from, TmqMessage deliveryMessage)
        {
            MessageDelivery delivery = TimeKeeper.FindAndRemoveDelivery(from, deliveryMessage.MessageId);

            //when server and consumer are in pc,
            //sometimes consumer sends ack before server start to follow ack of the message
            //that happens when ack message is arrived in less than 0.01ms
            //in that situation, server can't find the delivery with FindAndRemoveDelivery, it returns null
            //so we need to check it again after a few milliseconds
            if (delivery == null)
            {
                await Task.Delay(1);
                delivery = TimeKeeper.FindAndRemoveDelivery(from, deliveryMessage.MessageId);

                //try again
                if (delivery == null)
                {
                    await Task.Delay(3);
                    delivery = TimeKeeper.FindAndRemoveDelivery(from, deliveryMessage.MessageId);
                }
            }

            bool success = true;
            if (deliveryMessage.Length > 0 && deliveryMessage.Content != null)
            {
                string msg = deliveryMessage.Content.ToString();
                if (msg.Equals("FAILED", StringComparison.InvariantCultureIgnoreCase) || msg.Equals("TIMEOUT", StringComparison.InvariantCultureIgnoreCase))
                    success = false;
            }

            if (delivery != null)
                delivery.MarkAsAcknowledged(success);

            if (success)
                Info.AddAcknowledge();
            else
                Info.AddUnacknowledge();

            Decision decision = await DeliveryHandler.AcknowledgeReceived(this, deliveryMessage, delivery, success);

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