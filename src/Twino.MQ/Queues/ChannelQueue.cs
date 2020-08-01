using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Events;
using Twino.MQ.Options;
using Twino.MQ.Queues.States;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues
{
    /// <summary>
    /// Event handler for queues
    /// </summary>
    public delegate void QueueEventHandler(ChannelQueue queue);

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
        /// Tag name for the queue
        /// </summary>
        public string TagName
        {
            get => Options.TagName;
            set => Options.TagName = value;
        }

        /// <summary>
        /// Queue options.
        /// If null, channel default options will be used
        /// </summary>
        public ChannelQueueOptions Options { get; }

        /// <summary>
        /// Triggered when a message is produced 
        /// </summary>
        public MessageEventManager OnMessageProduced { get; private set; }

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
        public IEnumerable<QueueMessage> PriorityMessages => PriorityMessagesList;

        /// <summary>
        /// High priority message list
        /// </summary>
        internal readonly LinkedList<QueueMessage> PriorityMessagesList = new LinkedList<QueueMessage>();

        /// <summary>
        /// Standard priority queue message
        /// </summary>
        public IEnumerable<QueueMessage> Messages => MessagesList;

        /// <summary>
        /// Standard priority queue message
        /// </summary>
        internal readonly LinkedList<QueueMessage> MessagesList = new LinkedList<QueueMessage>();

        /// <summary>
        /// Time keeper for the queue.
        /// Checks message receiver deadlines and delivery deadlines.
        /// </summary>
        internal QueueTimeKeeper TimeKeeper { get; private set; }

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

        /// <summary>
        /// Triggered when queue is destroyed
        /// </summary>
        public event QueueEventHandler OnDestroyed;

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
            InitializeQueue();
        }

        internal ChannelQueue(Channel channel,
                              ushort id,
                              ChannelQueueOptions options)
        {
            Channel = channel;
            Id = id;
            Options = options;
            Status = options.Status;
        }

        /// <summary>
        /// Initializes queue to first use
        /// </summary>
        private void InitializeQueue()
        {
            State = QueueStateFactory.Create(this, Options.Status);
            OnMessageProduced = new MessageEventManager(Channel.Server, EventNames.MessageProduced, this);

            TimeKeeper = new QueueTimeKeeper(this);
            TimeKeeper.Run();

            if (Options.WaitForAcknowledge)
                _ackSync = new SemaphoreSlim(1, 1);

            _triggerTimer = new Timer(a =>
            {
                if (!_triggering && State.TriggerSupported)
                    _ = Trigger();
            }, null, TimeSpan.FromSeconds(5000), TimeSpan.FromSeconds(5000));
        }

        /// <summary>
        /// Sets message delivery handler and initializes queue
        /// </summary>
        internal void SetMessageDeliveryHandler(IMessageDeliveryHandler deliveryHandler)
        {
            if (DeliveryHandler != null)
                throw new InvalidOperationException("Queue has already a delivery handler");

            DeliveryHandler = deliveryHandler;
            InitializeQueue();
        }

        /// <summary>
        /// Destorys the queue
        /// </summary>
        public async Task Destroy()
        {
            try
            {
                await TimeKeeper.Destroy();
                OnMessageProduced.Dispose();

                lock (PriorityMessagesList)
                    PriorityMessagesList.Clear();

                lock (MessagesList)
                    MessagesList.Clear();

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
            finally
            {
                OnDestroyed?.Invoke(this);
            }
        }

        #endregion

        #region Messages

        /// <summary>
        /// Returns pending high priority messages count
        /// </summary>
        public int PriorityMessageCount()
        {
            return PriorityMessagesList.Count;
        }

        /// <summary>
        /// Returns pending regular messages count
        /// </summary>
        public int MessageCount()
        {
            return MessagesList.Count;
        }

        /// <summary>
        /// Finds and returns next queue message.
        /// Message will not be removed from the queue.
        /// If there is no message in queue, returns null
        /// </summary>
        public QueueMessage FindNextMessage()
        {
            if (PriorityMessagesList.Count > 0)
                lock (PriorityMessagesList)
                    return PriorityMessagesList.First?.Value;

            if (MessagesList.Count > 0)
                lock (MessagesList)
                    return MessagesList.First?.Value;

            return null;
        }

        /// <summary>
        /// Clears all messages in queue
        /// </summary>
        public void ClearRegularMessages()
        {
            lock (MessagesList)
                MessagesList.Clear();
        }

        /// <summary>
        /// Clears all messages in queue
        /// </summary>
        public void ClearHighPriorityMessages()
        {
            lock (PriorityMessagesList)
                PriorityMessagesList.Clear();
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
                        PriorityMessagesList.AddLast(message);
                    else
                        PriorityMessagesList.AddFirst(message);

                    Info.UpdateHighPriorityMessageCount(PriorityMessagesList.Count);
                }
                else
                {
                    if (putBack)
                        MessagesList.AddLast(message);
                    else
                        MessagesList.AddFirst(message);

                    Info.UpdateRegularMessageCount(MessagesList.Count);
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
                    PriorityMessagesList.Remove(message);
                else
                    MessagesList.Remove(message);
            }
            finally
            {
                _listSync.Release();
            }

            if (!silent)
            {
                Info.AddMessageRemove();
                await DeliveryHandler.MessageDequeued(this, message);
            }

            return true;
        }

        /// <summary>
        /// Adds new string message into the queue without Message Id and headers
        /// </summary>
        public void AddStringMessage(string messageContent)
        {
            AddStringMessage(null, messageContent);
        }

        /// <summary>
        /// Adds new string message into the queue with Message Id and returns the Id
        /// </summary>
        public string AddStringMessageWithId(string messageContent,
                                             bool firstAcquirer = false,
                                             bool priority = false,
                                             IEnumerable<KeyValuePair<string, string>> headers = null)

        {
            string messageId = Channel.Server.MessageIdGenerator.Create();
            AddStringMessage(messageId, messageContent, firstAcquirer, priority, headers);
            return messageId;
        }

        /// <summary>
        /// Adds new string message into the queue
        /// </summary>
        public void AddStringMessage(string messageId,
                                     string messageContent,
                                     bool firstAcquirer = false,
                                     bool priority = false,
                                     IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            AddBinaryMessage(messageId, Encoding.UTF8.GetBytes(messageContent), firstAcquirer, priority, headers);
        }

        /// <summary>
        /// Adds new binary message into the queue
        /// </summary>
        public void AddBinaryMessage(string messageId,
                                     byte[] messageContent,
                                     bool firstAcquirer = false,
                                     bool priority = false,
                                     IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            TmqMessage message = new TmqMessage(MessageType.QueueMessage, Channel.Name, Id);
            message.SetMessageId(messageId);
            message.Content = new MemoryStream(messageContent);
            message.FirstAcquirer = firstAcquirer;
            message.HighPriority = priority;

            if (headers != null)
                foreach (KeyValuePair<string, string> header in headers)
                    message.AddHeader(header.Key, header.Value);

            QueueMessage queueMessage = new QueueMessage(message);
            AddMessage(queueMessage);
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
                lock (PriorityMessagesList)
                {
                    //re-check when locked.
                    //it's checked before lock, because we dont wanna lock anything in non-concurrent already queued situations
                    if (message.IsInQueue)
                        return;

                    if (toEnd)
                        PriorityMessagesList.AddLast(message);
                    else
                        PriorityMessagesList.AddFirst(message);

                    message.IsInQueue = true;
                }

                Info.UpdateHighPriorityMessageCount(PriorityMessagesList.Count);
            }
            else
            {
                lock (MessagesList)
                {
                    //re-check when locked
                    //it's checked before lock, because we dont wanna lock anything in non-concurrent already queued situations
                    if (message.IsInQueue)
                        return;

                    if (toEnd)
                        MessagesList.AddLast(message);
                    else
                        MessagesList.AddFirst(message);

                    message.IsInQueue = true;
                }

                Info.UpdateRegularMessageCount(MessagesList.Count);
            }
        }

        /// <summary>
        /// Returns true, if there are no messages in queue
        /// </summary>
        public bool IsEmpty()
        {
            return PriorityMessagesList.Count == 0 && MessagesList.Count == 0;
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

            if (newDeliveryHandler != null)
                DeliveryHandler = newDeliveryHandler;

            if (Channel.Server.ChannelEventHandler != null)
                await Channel.Server.ChannelEventHandler.OnQueueStatusChanged(this, prevStatus, status);

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

            if (Options.MessageLimit > 0 && PriorityMessagesList.Count + MessagesList.Count >= Options.MessageLimit)
                return PushResult.LimitExceeded;

            if (Options.MessageSizeLimit > 0 && message.Message.Length > Options.MessageSizeLimit)
                return PushResult.LimitExceeded;

            //prepare properties
            message.Message.FirstAcquirer = true;
            message.Message.PendingAcknowledge = Options.RequestAcknowledge;

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

                //trigger message produced event
                OnMessageProduced.Trigger(message);

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

                        if (!State.ProcessingMessage.IsInQueue)
                        {
                            if (decision.PutBack == PutBackDecision.Start)
                                AddMessage(State.ProcessingMessage, false);
                            else if (decision.PutBack == PutBackDecision.End)
                                AddMessage(State.ProcessingMessage);
                        }
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

                if (PriorityMessagesList.Count > 0)
                    await ProcessPendingMessages(true);

                if (MessagesList.Count > 0)
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
                    lock (PriorityMessagesList)
                    {
                        if (PriorityMessagesList.Count == 0)
                            return;

                        message = PriorityMessagesList.First?.Value;
                        if (message == null)
                            return;

                        PriorityMessagesList.RemoveFirst();
                        message.IsInQueue = false;
                    }
                }
                else
                {
                    lock (MessagesList)
                    {
                        if (MessagesList.Count == 0)
                            return;

                        message = MessagesList.First?.Value;
                        if (message == null)
                            return;

                        MessagesList.RemoveFirst();
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
            PutBackDecision putBack = PutBackDecision.No;
            bool save = false;
            DeliveryAcknowledgeDecision ack = DeliveryAcknowledgeDecision.None;

            if (decision.Allow)
                allow = true;

            if (decision.PutBack != PutBackDecision.No)
                putBack = decision.PutBack;

            if (decision.SaveMessage)
                save = true;

            if (decision.Acknowledge == DeliveryAcknowledgeDecision.Always)
                ack = DeliveryAcknowledgeDecision.Always;

            else if (decision.Acknowledge == DeliveryAcknowledgeDecision.IfSaved && final.Acknowledge == DeliveryAcknowledgeDecision.None)
                ack = DeliveryAcknowledgeDecision.IfSaved;

            return new Decision(allow, save, putBack, ack);
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

            if (!message.IsProducerAckSent && (decision.Acknowledge == DeliveryAcknowledgeDecision.Always ||
                                               decision.Acknowledge == DeliveryAcknowledgeDecision.Negative ||
                                               decision.Acknowledge == DeliveryAcknowledgeDecision.IfSaved && message.IsSaved))
            {
                TmqMessage acknowledge = customAck ?? message.Message.CreateAcknowledge(decision.Acknowledge == DeliveryAcknowledgeDecision.Negative ? "none" : null);

                if (message.Source != null && message.Source.IsConnected)
                {
                    bool sent = await message.Source.SendAsync(acknowledge);
                    message.IsProducerAckSent = sent;
                    if (decision.AcknowledgeDelivery != null)
                        await decision.AcknowledgeDelivery(message, message.Source, sent);
                }
                else if (decision.AcknowledgeDelivery != null)
                    await decision.AcknowledgeDelivery(message, message.Source, false);
            }

            if (decision.PutBack == PutBackDecision.Start)
                AddMessage(message, false);
            else if (decision.PutBack == PutBackDecision.End)
                AddMessage(message);

            else if (!decision.Allow)
            {
                Info.AddMessageRemove();
                _ = DeliveryHandler.MessageDequeued(this, message);
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

        /// <summary>
        /// Sends decision to all connected master nodes
        /// </summary>
        public async Task SendDecisionToNodes(QueueMessage queueMessage, Decision decision)
        {
            TmqMessage msg = new TmqMessage(MessageType.Server, null, 10);
            DecisionOverNode model = new DecisionOverNode
                                     {
                                         Channel = Channel.Name,
                                         Queue = Id,
                                         MessageId = queueMessage.Message.MessageId,
                                         Acknowledge = decision.Acknowledge,
                                         Allow = decision.Allow,
                                         PutBack = decision.PutBack,
                                         SaveMessage = decision.SaveMessage
                                     };

            msg.Serialize(model, Channel.Server.MessageContentSerializer);
            Channel.Server.NodeManager.SendMessageToNodes(msg);
        }

        /// <summary>
        /// Applies decision over node to the queue
        /// </summary>
        internal async Task ApplyDecisionOverNode(string messageId, Decision decision)
        {
            QueueMessage message = null;
            await RunInListSync(() =>
            {
                //pull from prefential messages
                if (PriorityMessagesList.Count > 0)
                {
                    message = PriorityMessagesList.FirstOrDefault(x => x.Message.MessageId == messageId);
                    if (message != null)
                    {
                        message.IsInQueue = false;
                        PriorityMessagesList.Remove(message);
                    }
                }

                //if there is no prefential message, pull from standard messages
                if (message == null && MessagesList.Count > 0)
                {
                    message = MessagesList.FirstOrDefault(x => x.Message.MessageId == messageId);
                    if (message != null)
                    {
                        message.IsInQueue = false;
                        MessagesList.Remove(message);
                    }
                }
            });

            if (message == null)
                return;

            await ApplyDecision(decision, message);
        }

        #endregion

        #region Acknowledge

        /// <summary>
        /// When wait for acknowledge is active, this method locks the queue until acknowledge is received
        /// </summary>
        internal async Task WaitForAcknowledge(QueueMessage message)
        {
            //if we will lock the queue until ack received, we must request ack
            if (!message.Message.PendingAcknowledge)
                message.Message.PendingAcknowledge = true;

            if (_acknowledgeCallback == null)
                return;

            //lock the object, because pending ack message should be queued
            if (_ackSync == null)
                _ackSync = new SemaphoreSlim(1, 1);

            await _ackSync.WaitAsync();
            try
            {
                await _acknowledgeCallback.Task;
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

            bool success = !(deliveryMessage.HasHeader &&
                             deliveryMessage.Headers.Any(x => x.Key.Equals(TmqHeaders.NEGATIVE_ACKNOWLEDGE_REASON, StringComparison.InvariantCultureIgnoreCase)));

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse (it's possible, resharper doesn't work properly in here)
            if (delivery != null)
                delivery.MarkAsAcknowledged(success);

            if (success)
                Info.AddAcknowledge();
            else
                Info.AddNegativeAcknowledge();

            Decision decision = await DeliveryHandler.AcknowledgeReceived(this, deliveryMessage, delivery, success);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse (it's possible, resharper doesn't work properly in here)
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