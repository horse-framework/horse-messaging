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
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Queues.States;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues
{
    /// <summary>
    /// Event handler for queues
    /// </summary>
    public delegate void QueueEventHandler(TwinoQueue queue);

    /// <summary>
    /// Twino message queue.
    /// Keeps queued messages and subscribed clients.
    /// </summary>
    public class TwinoQueue
    {
        #region Properties

        /// <summary>
        /// Unique name (not case-sensetive)
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Queue topic
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Server of the queue
        /// </summary>
        public TwinoMQ Server { get; }

        /// <summary>
        /// Queue status
        /// </summary>
        public QueueStatus Status { get; private set; }

        /// <summary>
        /// Current status state object
        /// </summary>
        internal IQueueState State { get; private set; }

        /// <summary>
        /// Queue options.
        /// If null, queue default options will be used
        /// </summary>
        public QueueOptions Options { get; }

        /// <summary>
        /// Triggered when a message is produced 
        /// </summary>
        public MessageEventManager OnMessageProduced { get; private set; }

        /// <summary>
        /// Queue messaging handler.
        /// If null, server's default delivery will be used.
        /// </summary>
        public IMessageDeliveryHandler DeliveryHandler { get; set; }

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

        /// <summary>
        /// Clients in the queue as thread-unsafe list
        /// </summary>
        public IEnumerable<QueueClient> ClientsUnsafe => _clients.GetUnsafeList();

        /// <summary>
        /// Clients in the queue as cloned list
        /// </summary>
        public List<QueueClient> ClientsClone => _clients.GetAsClone();

        private readonly SafeList<QueueClient> _clients;

        /// <summary>
        /// Triggered when a client is subscribed 
        /// </summary>
        public SubscriptionEventManager OnConsumerSubscribed { get; private set; }

        /// <summary>
        /// Triggered when a client is unsubscribed 
        /// </summary>
        public SubscriptionEventManager OnConsumerUnsubscribed { get; private set; }

        /// <summary>
        /// True if queue is destroyed
        /// </summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>
        /// True if queue is initialized
        /// </summary>
        public bool IsInitialized { get; private set; }

        #endregion

        #region Constructors - Destroy

        internal TwinoQueue(TwinoMQ server, string name, QueueOptions options)
        {
            Server = server;
            Name = name;
            Options = options;
            Status = options.Status;
            _clients = new SafeList<QueueClient>(256);

            OnConsumerSubscribed = new SubscriptionEventManager(Server, EventNames.Subscribe, this);
            OnConsumerUnsubscribed = new SubscriptionEventManager(Server, EventNames.Unsubscribe, this);
            OnMessageProduced = new MessageEventManager(Server, EventNames.MessageProduced, this);
        }

        /// <summary>
        /// Initializes queue to first use
        /// </summary>
        internal void InitializeQueue(IMessageDeliveryHandler deliveryHandler = null)
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            if (deliveryHandler != null)
                DeliveryHandler = deliveryHandler;

            if (DeliveryHandler == null)
                throw new ArgumentNullException("Queue has no delivery handler: " + Name);

            State = QueueStateFactory.Create(this, Options.Status);

            TimeKeeper = new QueueTimeKeeper(this);
            TimeKeeper.Run();

            _ackSync = new SemaphoreSlim(1, 1);

            _triggerTimer = new Timer(a =>
            {
                if (!_triggering && State.TriggerSupported)
                    _ = Trigger();

                _ = CheckAutoDestroy();
            }, null, TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(5000));
        }

        /// <summary>
        /// Destorys the queue
        /// </summary>
        public async Task Destroy()
        {
            IsDestroyed = true;

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

            _clients.Clear();
            OnConsumerSubscribed.Dispose();
            OnConsumerUnsubscribed.Dispose();
        }

        /// <summary>
        /// If auto destroy is enabled, checks and removes queue if it should be removed
        /// </summary>
        internal async Task CheckAutoDestroy()
        {
            if (IsDestroyed || Options.AutoDestroy == QueueDestroy.Disabled)
                return;

            switch (Options.AutoDestroy)
            {
                case QueueDestroy.NoConsumers:
                    if (_clients.Count == 0)
                        await Server.RemoveQueue(this);

                    break;

                case QueueDestroy.NoMessages:
                    if (MessagesList.Count == 0 && PriorityMessagesList.Count == 0 && !TimeKeeper.HasPendingDelivery())
                        await Server.RemoveQueue(this);

                    break;

                case QueueDestroy.Empty:
                    if (_clients.Count == 0 && MessagesList.Count == 0 && PriorityMessagesList.Count == 0 && !TimeKeeper.HasPendingDelivery())
                        await Server.RemoveQueue(this);

                    break;
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
            string messageId = Server.MessageIdGenerator.Create();
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
            TwinoMessage message = new TwinoMessage(MessageType.QueueMessage, Name);
            message.SetMessageId(messageId);
            message.Content = new MemoryStream(messageContent);
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

            _ = Trigger();
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

            try
            {
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

                foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                    await handler.OnStatusChanged(this, prevStatus, status);

                if (enter == QueueStatusAction.AllowAndTrigger)
                    _ = Trigger();
            }
            catch (Exception e)
            {
                Server.SendError("SET_QUEUE_STATUS", e, $"QueueName:{Name}, PrevStatus:{prevStatus} NextStatus:{status}");
            }
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

        private void UpdateOptionsByMessage(TwinoMessage message)
        {
            string waitForAck = message.FindHeader(TwinoHeaders.ACKNOWLEDGE);
            if (!string.IsNullOrEmpty(waitForAck))
                switch (waitForAck.Trim().ToLower())
                {
                    case "none":
                        Options.Acknowledge = QueueAckDecision.None;
                        break;
                    case "request":
                        Options.Acknowledge = QueueAckDecision.JustRequest;
                        break;
                    case "wait":
                        Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                        break;
                }

            string queueStatus = message.FindHeader(TwinoHeaders.QUEUE_STATUS);
            if (queueStatus != null)
            {
                Options.Status = QueueStatusHelper.FindStatus(queueStatus);
            }

            Topic = message.FindHeader(TwinoHeaders.QUEUE_TOPIC);
        }

        #endregion

        #region Delivery

        /// <summary>
        /// Pushes a message into the queue.
        /// </summary>
        internal async Task<PushResult> Push(QueueMessage message, MqClient sender)
        {
            if (!IsInitialized)
            {
                try
                {
                    UpdateOptionsByMessage(message.Message);
                    DeliveryHandlerBuilder handlerBuilder = new DeliveryHandlerBuilder
                                                            {
                                                                Server = Server,
                                                                Queue = this,
                                                                Headers = message.Message.Headers,
                                                                DeliveryHandlerHeader = message.Message.FindHeader(TwinoHeaders.DELIVERY_HANDLER)
                                                            };

                    IMessageDeliveryHandler deliveryHandler = await Server.DeliveryHandlerFactory(handlerBuilder);
                    InitializeQueue(deliveryHandler);
                }
                catch (Exception e)
                {
                    Server.SendError("INITIALIZE_IN_PUSH", e, $"QueueName:{Name}");
                    throw;
                }
            }

            if (Status == QueueStatus.Stopped)
                return PushResult.StatusNotSupported;

            if (Options.MessageLimit > 0 && PriorityMessagesList.Count + MessagesList.Count >= Options.MessageLimit)
                return PushResult.LimitExceeded;

            if (Options.MessageSizeLimit > 0 && message.Message.Length > Options.MessageSizeLimit)
                return PushResult.LimitExceeded;

            //remove operational headers that are should not be sent to consumers or saved to disk
            message.Message.RemoveHeaders(TwinoHeaders.DELAY_BETWEEN_MESSAGES,
                                          TwinoHeaders.ACKNOWLEDGE,
                                          TwinoHeaders.QUEUE_STATUS,
                                          TwinoHeaders.QUEUE_TOPIC,
                                          TwinoHeaders.CC);

            //prepare properties
            message.Message.WaitResponse = Options.Acknowledge != QueueAckDecision.None;

            //if message doesn't have message id and "UseMessageId" option is enabled, create new message id for the message
            if (Options.UseMessageId && string.IsNullOrEmpty(message.Message.MessageId))
                message.Message.SetMessageId(Server.MessageIdGenerator.Create());

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
                Server.SendError("PUSH", ex, $"QueueName:{Name}");
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
        /// This method is called automatically after a client subscribed to the queue or status has changed.
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

                if (ClientsCount() == 0)
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
                    Server.SendError("PROCESS_MESSAGES", ex, $"QueueName:{Name}");
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

                if (Options.DelayBetweenMessages > 0)
                    await Task.Delay(Options.DelayBetweenMessages);
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
        internal async Task<bool> ApplyDecision(Decision decision, QueueMessage message, TwinoMessage customAck = null)
        {
            try
            {
                if (decision.SaveMessage)
                    await SaveMessage(message);

                if (!message.IsProducerAckSent && (decision.Acknowledge == DeliveryAcknowledgeDecision.Always ||
                                                   decision.Acknowledge == DeliveryAcknowledgeDecision.Negative ||
                                                   decision.Acknowledge == DeliveryAcknowledgeDecision.IfSaved && message.IsSaved))
                {
                    TwinoMessage acknowledge = customAck ?? message.Message.CreateAcknowledge(decision.Acknowledge == DeliveryAcknowledgeDecision.Negative ? "none" : null);

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

                if (decision.PutBack != PutBackDecision.No)
                    ApplyPutBack(decision, message);
                else if (!decision.Allow)
                {
                    Info.AddMessageRemove();
                    _ = DeliveryHandler.MessageDequeued(this, message);
                }
            }
            catch (Exception e)
            {
                Server.SendError("APPLY_DECISION", e, $"QueueName:{Name}, MessageId:{message.Message.MessageId}");
            }

            return decision.Allow;
        }

        /// <summary>
        /// Executes put back decision for the message
        /// </summary>
        private void ApplyPutBack(Decision decision, QueueMessage message)
        {
            switch (decision.PutBack)
            {
                case PutBackDecision.Start:
                    AddMessage(message, false);
                    break;

                case PutBackDecision.StartDelayed:
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(Options.PutBackDelay);
                            AddMessage(message, false);
                        }
                        catch (Exception e)
                        {
                            Server.SendError("APPLY_DECISION", e, $"QueueName:{Name}, MessageId:{message.Message.MessageId}");
                        }
                    });
                    break;

                case PutBackDecision.End:
                    AddMessage(message);
                    break;

                case PutBackDecision.EndDelayed:
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(Options.PutBackDelay);
                            AddMessage(message);
                        }
                        catch (Exception e)
                        {
                            Server.SendError("APPLY_DECISION", e, $"QueueName:{Name}, MessageId:{message.Message.MessageId}");
                        }
                    });
                    break;
            }
        }

        /// <summary>
        /// Saves message
        /// </summary>
        public async Task<bool> SaveMessage(QueueMessage message)
        {
            try
            {
                if (message.IsSaved)
                    return false;

                if (IsInitialized)
                    message.IsSaved = await DeliveryHandler.SaveMessage(this, message);

                if (message.IsSaved)
                    Info.AddMessageSave();
            }
            catch (Exception e)
            {
                Server.SendError("SAVE_MESSAGE", e, $"QueueName:{Name}, MessageId:{message.Message.MessageId}");
            }

            return message.IsSaved;
        }

        /// <summary>
        /// Sends decision to all connected master nodes
        /// </summary>
        public void SendDecisionToNodes(QueueMessage queueMessage, Decision decision)
        {
            TwinoMessage msg = new TwinoMessage(MessageType.Server, null, 10);
            DecisionOverNode model = new DecisionOverNode
                                     {
                                         Queue = Name,
                                         MessageId = queueMessage.Message.MessageId,
                                         Acknowledge = decision.Acknowledge,
                                         Allow = decision.Allow,
                                         PutBack = decision.PutBack,
                                         SaveMessage = decision.SaveMessage
                                     };

            msg.Serialize(model, Server.MessageContentSerializer);
            Server.NodeManager.SendMessageToNodes(msg);
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
            if (!message.Message.WaitResponse)
                message.Message.WaitResponse = true;

            await _ackSync.WaitAsync();
            try
            {
                if (_acknowledgeCallback != null)
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
        internal async Task AcknowledgeDelivered(MqClient from, TwinoMessage deliveryMessage)
        {
            try
            {
                if (!IsInitialized)
                    return;

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
                                 deliveryMessage.Headers.Any(x => x.Key.Equals(TwinoHeaders.NEGATIVE_ACKNOWLEDGE_REASON, StringComparison.InvariantCultureIgnoreCase)));

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
            catch (Exception e)
            {
                Server.SendError("QUEUE_ACK_RECEIVED", e, $"QueueName:{Name}, MessageId:{deliveryMessage.MessageId}");
            }
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

        #region Client Actions

        /// <summary>
        /// Returns client count in the queue
        /// </summary>
        /// <returns></returns>
        public int ClientsCount()
        {
            return _clients.Count;
        }

        /// <summary>
        /// Adds the client to the queue
        /// </summary>
        public async Task<QueueSubscriptionResult> AddClient(MqClient client)
        {
            foreach (IQueueAuthenticator authenticator in Server.QueueAuthenticators)
            {
                bool allowed = await authenticator.Authenticate(this, client);
                if (!allowed)
                    return QueueSubscriptionResult.Unauthorized;
            }

            if (Options.ClientLimit > 0 && _clients.Count >= Options.ClientLimit)
                return QueueSubscriptionResult.Full;

            QueueClient cc = new QueueClient(this, client);
            _clients.Add(cc);
            client.AddSubscription(cc);

            foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                await handler.OnConsumerSubscribed(cc);

            _ = Trigger();
            OnConsumerSubscribed.Trigger(cc);
            return QueueSubscriptionResult.Success;
        }

        /// <summary>
        /// Removes client from the queue
        /// </summary>
        public async Task RemoveClient(QueueClient client)
        {
            _clients.Remove(client);
            client.Client.RemoveSubscription(client);

            foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                await handler.OnConsumerUnsubscribed(client);

            OnConsumerUnsubscribed.Trigger(client);
        }

        /// <summary>
        /// Removes client from the queue, does not call MqClient's remove method
        /// </summary>
        internal async Task RemoveClientSilent(QueueClient client)
        {
            _clients.Remove(client);

            foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                await handler.OnConsumerUnsubscribed(client);

            OnConsumerUnsubscribed.Trigger(client);
        }

        /// <summary>
        /// Removes client from the queue
        /// </summary>
        public async Task<bool> RemoveClient(MqClient client)
        {
            QueueClient cc = _clients.FindAndRemove(x => x.Client == client);

            if (cc == null)
                return false;

            client.RemoveSubscription(cc);

            foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                await handler.OnConsumerUnsubscribed(cc);

            OnConsumerUnsubscribed.Trigger(cc);
            return true;
        }

        /// <summary>
        /// Finds client in the queue
        /// </summary>
        public QueueClient FindClient(string uniqueId)
        {
            return _clients.Find(x => x.Client.UniqueId == uniqueId);
        }

        /// <summary>
        /// Finds client in the queue
        /// </summary>
        public QueueClient FindClient(MqClient client)
        {
            return _clients.Find(x => x.Client == client);
        }

        /// <summary>
        /// Gets next client with round robin algorithm and updates index
        /// </summary>
        internal QueueClient GetNextRRClient(ref int index)
        {
            List<QueueClient> clients = _clients.GetAsClone();
            if (index < 0 || index + 1 >= clients.Count)
            {
                if (clients.Count == 0)
                    return null;

                index = 0;
                return clients[0];
            }

            index++;
            return clients[index];
        }

        #endregion
    }
}