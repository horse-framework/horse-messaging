using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.States;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Event handler for queues
    /// </summary>
    public delegate void QueueEventHandler(HorseQueue queue);

    /// <summary>
    /// Horse message queue.
    /// Keeps queued messages and subscribed clients.
    /// </summary>
    public class HorseQueue
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
        public HorseMq Server { get; }

        /// <summary>
        /// Queue status
        /// </summary>
        public QueueStatus Status { get; private set; }

        /// <summary>
        /// Queue type
        /// </summary>
        public QueueType Type { get; private set; }

        /// <summary>
        /// Current status state object
        /// </summary>
        internal IQueueState State { get; private set; }

        /// <summary>
        /// Message store of the queue
        /// </summary>
        internal IQueueMessageStore Store { get; private set; }

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
        /// Returns currently processing message.
        /// The message is about to send, but it might be waiting for acknowledge of previous message or delay between messages.
        /// </summary>
        public QueueMessage ProcessingMessage => State?.ProcessingMessage;

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

        #endregion

        #region Constructors - Destroy

        internal HorseQueue(HorseMq server, string name, QueueOptions options)
        {
            Server = server;
            Name = name;
            Options = options;
            Type = options.Type;
            _clients = new SafeList<QueueClient>(256);
            Status = QueueStatus.NotInitialized;

            OnConsumerSubscribed = new SubscriptionEventManager(Server, EventNames.Subscribe, this);
            OnConsumerUnsubscribed = new SubscriptionEventManager(Server, EventNames.Unsubscribe, this);
            OnMessageProduced = new MessageEventManager(Server, EventNames.MessageProduced, this);
        }

        /// <summary>
        /// Sets queue status
        /// </summary>
        public void SetStatus(QueueStatus newStatus)
        {
            if (newStatus == QueueStatus.NotInitialized)
                return;

            Status = newStatus;
        }
        
        /// <summary>
        /// Initializes queue to first use
        /// </summary>
        internal Task InitializeQueue(IMessageDeliveryHandler deliveryHandler = null)
        {
            return RunInListSync(() =>
            {
                if (Status != QueueStatus.NotInitialized)
                    return;

                if (deliveryHandler != null)
                    DeliveryHandler = deliveryHandler;

                if (DeliveryHandler == null)
                    throw new ArgumentNullException("Queue has no delivery handler: " + Name);

                var tuple = QueueStateFactory.Create(this, Options.Type);
                State = tuple.Item1;
                Store = tuple.Item2;

                TimeKeeper = new QueueTimeKeeper(this);
                TimeKeeper.Run();

                _ackSync = new SemaphoreSlim(1, 1);
                Status = QueueStatus.Running;
                
                _triggerTimer = new Timer(a =>
                {
                    if (!_triggering && State.TriggerSupported)
                        _ = Trigger();

                    _ = CheckAutoDestroy();
                }, null, TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(5000));
            });
        }

        /// <summary>
        /// Destorys the queue
        /// </summary>
        public async Task Destroy()
        {
            IsDestroyed = true;

            try
            {
                if (TimeKeeper != null)
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
                    if (MessagesList.Count == 0 && PriorityMessagesList.Count == 0 && (TimeKeeper == null || !TimeKeeper.HasPendingDelivery()))
                        await Server.RemoveQueue(this);

                    break;

                case QueueDestroy.Empty:
                    if (_clients.Count == 0 && MessagesList.Count == 0 && PriorityMessagesList.Count == 0 && (TimeKeeper == null || !TimeKeeper.HasPendingDelivery()))
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
        /// Returns unique pending message count
        /// </summary>
        public int GetAckPendingMessageCount()
        {
            if (TimeKeeper == null)
                return 0;

            return TimeKeeper.GetPendingMessageCount();
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
                }
                else
                {
                    if (putBack)
                        MessagesList.AddLast(message);
                    else
                        MessagesList.AddFirst(message);
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

        internal void UpdateOptionsByMessage(HorseMessage message)
        {
            if (!message.HasHeader)
                return;

            foreach (KeyValuePair<string, string> pair in message.Headers)
            {
                if (pair.Key.Equals(HorseHeaders.ACKNOWLEDGE, StringComparison.InvariantCultureIgnoreCase))
                {
                    switch (pair.Value.Trim().ToLower())
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
                }

                else if (pair.Key.Equals(HorseHeaders.QUEUE_TYPE, StringComparison.InvariantCultureIgnoreCase))
                    Options.Type = pair.Value.ToQueueType();

                else if (pair.Key.Equals(HorseHeaders.QUEUE_TOPIC, StringComparison.InvariantCultureIgnoreCase))
                    Topic = pair.Value;

                else if (pair.Key.Equals(HorseHeaders.PUT_BACK_DELAY, StringComparison.InvariantCultureIgnoreCase))
                    Options.PutBackDelay = Convert.ToInt32(pair.Value);

                else if (pair.Key.Equals(HorseHeaders.MESSAGE_TIMEOUT, StringComparison.InvariantCultureIgnoreCase))
                    Options.MessageTimeout = TimeSpan.FromSeconds(Convert.ToInt32(pair.Value));

                else if (pair.Key.Equals(HorseHeaders.ACK_TIMEOUT, StringComparison.InvariantCultureIgnoreCase))
                    Options.AcknowledgeTimeout = TimeSpan.FromSeconds(Convert.ToInt32(pair.Value));

                else if (pair.Key.Equals(HorseHeaders.DELAY_BETWEEN_MESSAGES, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(pair.Value))
                        Options.DelayBetweenMessages = Convert.ToInt32(pair.Value);
                }
            }
        }

        #endregion

        #region Delivery

        /// <summary>
        /// Pushes new message into the queue
        /// </summary>
        public Task<PushResult> Push(string message, bool highPriority = false)
        {
            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, Name);
            msg.HighPriority = highPriority;
            msg.SetStringContent(message);
            return Push(msg);
        }

        /// <summary>
        /// Pushes new message into the queue
        /// </summary>
        public Task<PushResult> Push(HorseMessage message)
        {
            message.Type = MessageType.QueueMessage;
            message.SetTarget(Name);

            return Push(new QueueMessage(message), null);
        }

        /// <summary>
        /// Pushes a message into the queue.
        /// </summary>
        internal async Task<PushResult> Push(QueueMessage message, MessagingClient sender)
        {
            if (Status == QueueStatus.NotInitialized)
            {
                try
                {
                    UpdateOptionsByMessage(message.Message);
                    DeliveryHandlerBuilder handlerBuilder = new DeliveryHandlerBuilder
                                                            {
                                                                Server = Server,
                                                                Queue = this,
                                                                Headers = message.Message.Headers,
                                                                DeliveryHandlerHeader = message.Message.FindHeader(HorseHeaders.DELIVERY_HANDLER)
                                                            };

                    IMessageDeliveryHandler deliveryHandler = await Server.DeliveryHandlerFactory(handlerBuilder);
                    await InitializeQueue(deliveryHandler);

                    handlerBuilder.TriggerAfterCompleted();
                }
                catch (Exception e)
                {
                    Server.SendError("INITIALIZE_IN_PUSH", e, $"QueueName:{Name}");
                    throw;
                }
            }

            if (Status is QueueStatus.OnlyConsume or QueueStatus.Paused)
                return PushResult.StatusNotSupported;

            if (Options.MessageLimit > 0 && PriorityMessagesList.Count + MessagesList.Count >= Options.MessageLimit)
                return PushResult.LimitExceeded;

            if (Options.MessageSizeLimit > 0 && message.Message.Length > Options.MessageSizeLimit)
                return PushResult.LimitExceeded;

            //remove operational headers that are should not be sent to consumers or saved to disk
            message.Message.RemoveHeaders(HorseHeaders.DELAY_BETWEEN_MESSAGES,
                                          HorseHeaders.ACKNOWLEDGE,
                                          HorseHeaders.QUEUE_NAME,
                                          HorseHeaders.QUEUE_TYPE,
                                          HorseHeaders.QUEUE_TOPIC,
                                          HorseHeaders.PUT_BACK_DELAY,
                                          HorseHeaders.DELIVERY,
                                          HorseHeaders.DELIVERY_HANDLER,
                                          HorseHeaders.MESSAGE_TIMEOUT,
                                          HorseHeaders.ACK_TIMEOUT,
                                          HorseHeaders.CC);

            //prepare properties
            message.Message.WaitResponse = Options.Acknowledge != QueueAckDecision.None;

            //if message doesn't have message id, create new message id for the message
            if (string.IsNullOrEmpty(message.Message.MessageId))
                message.Message.SetMessageId(Server.MessageIdGenerator.Create());

            //if we have an option maximum wait duration for message, set it after message joined to the queue.
            //time keeper will check this value and if message time is up, it will remove message from the queue.
            if (Options.MessageTimeout > TimeSpan.Zero)
                message.Deadline = DateTime.UtcNow.Add(Options.MessageTimeout);

            try
            {
                //fire message receive event
                Info.AddMessageReceive();
                Decision decision = await DeliveryHandler.ReceivedFromProducer(this, message, sender);
                message.Decision = decision;

                bool allow = await ApplyDecision(decision, message);

                foreach (IQueueMessageEventHandler handler in Server.QueueMessageHandlers)
                    _ = handler.OnProduced(this, message, sender);

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

                    //the message is removed from the queue and it's not sent to consumers
                    //we should put the message back into the queue
                    if (!message.IsInQueue && !message.IsSent && State.CanEnqueue(message))
                        decision = new Decision(true, true, PutBackDecision.Start, DeliveryAcknowledgeDecision.None);

                    if (State.ProcessingMessage != null)
                        await ApplyDecision(decision, State.ProcessingMessage);
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

                if (_clients.Count == 0)
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
                if (_clients.Count == 0)
                    return;

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
                    if (pr == PushResult.NoConsumers || pr == PushResult.Empty)
                        return;
                }
                catch (Exception ex)
                {
                    Server.SendError("PROCESS_MESSAGES", ex, $"QueueName:{Name}");
                    Info.AddError();
                    try
                    {
                        Decision decision = await DeliveryHandler.ExceptionThrown(this, message, ex);

                        //the message is removed from the queue and it's not sent to consumers
                        //we should put the message back into the queue
                        if (!message.IsInQueue && !message.IsSent)
                            decision = new Decision(true, true, PutBackDecision.Start, DeliveryAcknowledgeDecision.None);

                        await ApplyDecision(decision, message, null, 5000);
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
        internal async Task<bool> ApplyDecision(Decision decision, QueueMessage message, HorseMessage customAck = null, int forceDelay = 0)
        {
            try
            {
                if (decision.SaveMessage)
                    await SaveMessage(message);

                if (!message.IsProducerAckSent && (decision.Acknowledge == DeliveryAcknowledgeDecision.Always ||
                                                   decision.Acknowledge == DeliveryAcknowledgeDecision.Negative ||
                                                   decision.Acknowledge == DeliveryAcknowledgeDecision.IfSaved && message.IsSaved))
                {
                    HorseMessage acknowledge = customAck ?? message.Message.CreateAcknowledge(decision.Acknowledge == DeliveryAcknowledgeDecision.Negative ? "none" : null);

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
                    ApplyPutBack(decision, message, forceDelay);
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
        internal void ApplyPutBack(Decision decision, QueueMessage message, int forceDelay = 0)
        {
            switch (decision.PutBack)
            {
                case PutBackDecision.Start:
                    if (Options.PutBackDelay == 0)
                        AddMessage(message, false);
                    else
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(Options.PutBackDelay);
                                AddMessage(message, false);
                            }
                            catch (Exception e)
                            {
                                Server.SendError("DELAYED_PUT_BACK", e, $"QueueName:{Name}, MessageId:{message.Message.MessageId}");
                            }
                        });
                    break;

                case PutBackDecision.End:
                    if (Options.PutBackDelay == 0 && forceDelay == 0)
                        AddMessage(message);
                    else
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(Math.Max(forceDelay, Options.PutBackDelay));
                                AddMessage(message);
                            }
                            catch (Exception e)
                            {
                                Server.SendError("DELAYED_PUT_BACK", e, $"QueueName:{Name}, MessageId:{message.Message.MessageId}");
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

                if (Status != QueueStatus.NotInitialized)
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
            HorseMessage msg = new HorseMessage(MessageType.Server, null, 10);
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
        internal async Task AcknowledgeDelivered(MessagingClient from, HorseMessage deliveryMessage)
        {
            try
            {
                if (Status == QueueStatus.NotInitialized)
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
                                 deliveryMessage.Headers.Any(x => x.Key.Equals(HorseHeaders.NEGATIVE_ACKNOWLEDGE_REASON, StringComparison.InvariantCultureIgnoreCase)));

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse (it's possible, resharper doesn't work properly in here)
                if (delivery != null)
                {
                    if (delivery.Receiver != null && delivery.Message == delivery.Receiver.CurrentlyProcessing)
                        delivery.Receiver.CurrentlyProcessing = null;

                    delivery.MarkAsAcknowledged(success);
                }

                if (success)
                    Info.AddAcknowledge();
                else
                    Info.AddNegativeAcknowledge();

                Decision decision = await DeliveryHandler.AcknowledgeReceived(this, deliveryMessage, delivery, success);

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse (it's possible, resharper doesn't work properly in here)
                if (delivery != null)
                    await ApplyDecision(decision, delivery.Message, deliveryMessage);

                foreach (IQueueMessageEventHandler handler in Server.QueueMessageHandlers)
                    _ = handler.OnAcknowledged(this, deliveryMessage, delivery, success);

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
        public async Task<SubscriptionResult> AddClient(MessagingClient client)
        {
            foreach (IQueueAuthenticator authenticator in Server.QueueAuthenticators)
            {
                bool allowed = await authenticator.Authenticate(this, client);
                if (!allowed)
                    return SubscriptionResult.Unauthorized;
            }

            if (Options.ClientLimit > 0 && _clients.Count >= Options.ClientLimit)
                return SubscriptionResult.Full;

            QueueClient cc = new QueueClient(this, client);
            _clients.Add(cc);
            client.AddSubscription(cc);

            foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                _ = handler.OnConsumerSubscribed(cc);

            _ = Trigger();
            OnConsumerSubscribed.Trigger(cc);
            return SubscriptionResult.Success;
        }

        /// <summary>
        /// Removes client from the queue
        /// </summary>
        public void RemoveClient(QueueClient client)
        {
            _clients.Remove(client);
            client.Client.RemoveSubscription(client);

            foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                _ = handler.OnConsumerUnsubscribed(client);

            OnConsumerUnsubscribed.Trigger(client);
        }

        /// <summary>
        /// Removes client from the queue, does not call MqClient's remove method
        /// </summary>
        internal void RemoveClientSilent(QueueClient client)
        {
            _clients.Remove(client);

            foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                _ = handler.OnConsumerUnsubscribed(client);

            OnConsumerUnsubscribed.Trigger(client);
        }

        /// <summary>
        /// Removes client from the queue
        /// </summary>
        public bool RemoveClient(MessagingClient client)
        {
            QueueClient cc = _clients.FindAndRemove(x => x.Client == client);

            if (cc == null)
                return false;

            client.RemoveSubscription(cc);

            foreach (IQueueEventHandler handler in Server.QueueEventHandlers)
                _ = handler.OnConsumerUnsubscribed(cc);

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
        public QueueClient FindClient(MessagingClient client)
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

        /// <summary>
        /// Gets next available client which is not currently consuming any message.
        /// Used for wait for acknowledge situations
        /// </summary>
        internal async Task<Tuple<QueueClient, int>> GetNextAvailableRRClient(int currentIndex)
        {
            List<QueueClient> clients = _clients.GetAsClone();
            if (clients.Count == 0)
                return new Tuple<QueueClient, int>(null, currentIndex);

            DateTime retryExpiration = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                int index = currentIndex < 0 ? 0 : currentIndex;
                for (int i = 0; i < clients.Count; i++)
                {
                    if (index >= clients.Count)
                        index = 0;

                    QueueClient client = clients[index];
                    if (client.CurrentlyProcessing == null)
                        return new Tuple<QueueClient, int>(client, index);

                    if (client.CurrentlyProcessing.Deadline < DateTime.UtcNow)
                    {
                        client.CurrentlyProcessing = null;
                        return new Tuple<QueueClient, int>(client, index);
                    }

                    index++;
                }

                await Task.Delay(3);
                clients = _clients.GetAsClone();
                if (clients.Count == 0)
                    break;

                //don't try hard so much, wait for next trigger operation of the queue.
                //it will be triggered in 5 secs, anyway
                if (DateTime.UtcNow > retryExpiration)
                    break;
            }

            return new Tuple<QueueClient, int>(null, currentIndex);
        }

        #endregion
    }
}