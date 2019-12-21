using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Options;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues
{
    /// <summary>
    /// Queue status
    /// </summary>
    public enum QueueStatus
    {
        /// <summary>
        /// Queue messaging is in running state.
        /// Messages are not queued, producers push the message and if there are available consumers, message is sent to them.
        /// Otherwise, message is deleted.
        /// If you need to keep messages and transmit only live messages, Route is good status to consume less resource.
        /// </summary>
        Route,

        /// <summary>
        /// Queue messaging is in running state.
        /// Producers push the message into the queue and consumer receive when message is pushed
        /// </summary>
        Push,

        /// <summary>
        /// Queue messaging is in running state.
        /// Producers push message into queue, consumers receive the messages when they requested.
        /// Each message is sent only one-receiver at same time.
        /// Request operation removes the message from the queue.
        /// </summary>
        Pull,

        /// <summary>
        /// Queue messages are accepted from producers but they are not sending to consumers even they request new messages. 
        /// </summary>
        Paused,

        /// <summary>
        /// Queue messages are removed, producers can't push any message to the queue and consumers can't receive any message
        /// </summary>
        Stopped
    }

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
        /// Queue content type
        /// </summary>
        public ushort ContentType { get; }

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
        /// High priority message list
        /// </summary>
        private readonly LinkedList<QueueMessage> _prefentialMessages = new LinkedList<QueueMessage>();

        /// <summary>
        /// Low/Standard priority message list
        /// </summary>
        private readonly LinkedList<QueueMessage> _standardMessages = new LinkedList<QueueMessage>();

        /// <summary>
        /// Standard prefential messages
        /// </summary>
        public IEnumerable<QueueMessage> PrefentialMessages => _prefentialMessages;

        /// <summary>
        /// Standard queued messages
        /// </summary>
        public IEnumerable<QueueMessage> StandardMessages => _standardMessages;

        /// <summary>
        /// Default TMQ Writer class for the queue
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

        /// <summary>
        /// Time keeper for the queue.
        /// Checks message receiver deadlines and delivery deadlines.
        /// </summary>
        private readonly QueueTimeKeeper _timeKeeper;

        /// <summary>
        /// 
        /// </summary>
        private readonly SemaphoreSlim _semaphore;

        /// <summary>
        /// Waiting acknowledge status.
        /// This value will be true right after a message is sent, until it's delivery process completed (timeout or ack)
        /// </summary>
        private volatile bool _waitingAcknowledge;

        #endregion

        #region Constructors

        internal ChannelQueue(Channel channel,
                              ushort contentType,
                              ChannelQueueOptions options,
                              IMessageDeliveryHandler deliveryHandler)
        {
            Channel = channel;
            ContentType = contentType;
            Options = options;
            Status = options.Status;
            DeliveryHandler = deliveryHandler;

            _timeKeeper = new QueueTimeKeeper(this, _prefentialMessages, _standardMessages);
            _timeKeeper.Run();

            if (options.WaitForAcknowledge)
                _semaphore = new SemaphoreSlim(1, 1024);
        }

        /// <summary>
        /// Destorys the queue
        /// </summary>
        public async Task Destroy()
        {
            await _timeKeeper.Destroy();

            lock (_prefentialMessages)
                _prefentialMessages.Clear();

            lock (_standardMessages)
                _standardMessages.Clear();
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
                lock (_prefentialMessages)
                    _prefentialMessages.Clear();

                lock (_standardMessages)
                    _standardMessages.Clear();

                _timeKeeper.Reset();
            }

            Status = status;

            //trigger queued messages
            if (status == QueueStatus.Route || status == QueueStatus.Push)
                await Trigger(null);
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

        #region Messaging Actions

        /// <summary>
        /// Removes the message from the queue.
        /// Remove operation will be canceled If force is false and message is not sent
        /// </summary>
        public async Task<bool> RemoveMessage(QueueMessage message, bool force = false)
        {
            if (!force && !message.IsSent)
                return false;

            if (message.Message.HighPriority)
                _prefentialMessages.Remove(message);
            else
                _standardMessages.Remove(message);

            if (DeliveryHandler != null)
                await DeliveryHandler.MessageRemoved(this, message);

            return true;
        }

        /// <summary>
        /// Client pulls a message from the queue
        /// </summary>
        internal async Task Pull(ChannelClient client)
        {
            if (Status != QueueStatus.Pull)
                return;

            QueueMessage message = null;

            //pull from prefential messages
            if (_prefentialMessages.Count > 0)
                lock (_prefentialMessages)
                {
                    message = _prefentialMessages.First.Value;
                    _prefentialMessages.RemoveFirst();
                }

            //if there is no prefential message, pull from standard messages
            if (message == null && _standardMessages.Count > 0)
            {
                lock (_standardMessages)

                {
                    message = _standardMessages.First.Value;
                    _standardMessages.RemoveFirst();
                }
            }

            //there is no pullable message
            if (message == null)
                return;

            try
            {
                await ProcesssMessage(message, true, client);
            }
            catch (Exception ex)
            {
                try
                {
                    _ = DeliveryHandler.ExceptionThrown(this, message, ex);
                }
                catch //if developer does wrong operation, we should not stop
                {
                }
            }
        }

        /// <summary>
        /// Pushes a message into the queue.
        /// </summary>
        internal async Task<bool> Push(QueueMessage message, MqClient sender)
        {
            if (Status == QueueStatus.Stopped)
                return false;

            //prepare properties
            message.Message.FirstAcquirer = true;
            message.Message.AcknowledgeRequired = Options.RequestAcknowledge;

            if (Options.HideClientNames)
            {
                message.Message.Source = null;
                message.Message.SourceLength = 0;
            }

            //process the message
            QueueMessage held = null;
            try
            {
                //fire message receive event
                Decision decision = await DeliveryHandler.ReceivedFromProducer(this, message, sender);
                bool allow = await ApplyDecision(decision, message);
                if (!allow)
                    return true;

                //if we have an option maximum wait duration for message, set it after message joined to the queue.
                //time keeper will check this value and if message time is up, it will remove message from the queue.
                if (Options.MessageTimeout > TimeSpan.Zero)
                    message.Deadline = DateTime.UtcNow.Add(Options.MessageTimeout);

                //if message doesn't have message id and "UseMessageId" option is enabled, create new message id for the message
                if (Options.UseMessageId && string.IsNullOrEmpty(message.Message.MessageId))
                    message.Message.MessageId = Channel.Server.MessageIdGenerator.Create();

                switch (Status)
                {
                    //just send the message to receivers
                    case QueueStatus.Route:
                        held = message;
                        await ProcesssMessage(message, false);
                        break;

                    //keep the message in queue send send it to receivers
                    //if there is no receiver, message will kept back in the queue
                    case QueueStatus.Push:
                        held = PullMessage(message);
                        await ProcesssMessage(held, true);
                        break;

                    //dont send the message, just put it to queue
                    case QueueStatus.Pull:
                    case QueueStatus.Paused:
                        if (message.Message.HighPriority)
                            lock (_prefentialMessages)
                                _prefentialMessages.AddLast(message);
                        else
                            lock (_standardMessages)
                                _standardMessages.AddLast(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _ = DeliveryHandler.ExceptionThrown(this, held, ex);
                }
                catch //if developer does wrong operation, we should not stop
                {
                }
            }

            return true;
        }

        /// <summary>
        /// Checks all pending messages and subscribed receivers.
        /// If they should receive the messages, runs the process. 
        /// Should be called when a new client is subscribed to the channel.
        /// </summary>
        internal async Task Trigger(ChannelClient subscribedClient)
        {
            if (Status != QueueStatus.Route && Status != QueueStatus.Push)
                return;

            if (_prefentialMessages.Count > 0)
                await ProcessPendingMessages(_prefentialMessages);

            if (_standardMessages.Count > 0)
                await ProcessPendingMessages(_standardMessages);
        }

        /// <summary>
        /// Start to process all pending messages.
        /// This method is called after a client is subscribed to the queue.
        /// </summary>
        private async Task ProcessPendingMessages(LinkedList<QueueMessage> list)
        {
            while (true)
            {
                QueueMessage message;
                lock (list)
                {
                    if (list.Count == 0)
                        return;

                    message = list.First.Value;
                    list.RemoveFirst();
                }

                try
                {
                    await ProcesssMessage(message, true);
                }
                catch (Exception ex)
                {
                    try
                    {
                        _ = DeliveryHandler.ExceptionThrown(this, message, ex);
                    }
                    catch //if developer does wrong operation, we should not stop
                    {
                    }
                }
            }
        }

        /// <summary>
        /// When wait for acknowledge is active, this method locks the queue until acknowledge is received
        /// </summary>
        private async Task WaitForAcknowledge(QueueMessage message)
        {
            //if we will lock the queue until ack received, we must request ack
            if (!message.Message.AcknowledgeRequired)
                message.Message.AcknowledgeRequired = true;

            //lock the object, because pending ack message should be queued
            await _semaphore.WaitAsync();

            try
            {
                //if there is no queue in ack delivery
                //this message is sent and sets the waiting true until it's process completed
                if (!_waitingAcknowledge)
                {
                    _waitingAcknowledge = true;
                    return;
                }

                //if waiting already true, this message should wait until delivery process completed
                while (_waitingAcknowledge)
                    await Task.Delay(1);

                //now, it's this message turn, set wait true and go on.
                _waitingAcknowledge = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Searches receivers of the message and process the send operation
        /// </summary>
        private async Task ProcesssMessage(QueueMessage message, bool onheld, ChannelClient singleClient = null)
        {
            //if we need acknowledge, we are sending this information to receivers that we require response
            message.Message.AcknowledgeRequired = Options.RequestAcknowledge;

            //if we need acknowledge from receiver, it has a deadline.
            DateTime? deadline = null;
            if (Options.RequestAcknowledge)
                deadline = DateTime.UtcNow.Add(Options.AcknowledgeTimeout);

            //if to process next message is requires previous message acknowledge, wait here
            if (Options.RequestAcknowledge && Options.WaitForAcknowledge)
                await WaitForAcknowledge(message);

            Decision decision = await DeliveryHandler.BeginSend(this, message);
            bool allow = await ApplyDecision(decision, message);

            if (!allow)
            {
                message.Decision = decision;
                message.IsSkipped = true;
                message.Decision = await DeliveryHandler.EndSend(this, message);

                if (Status != QueueStatus.Route && onheld && message.Decision.KeepMessage)
                    PutMessageBack(message);
                else
                    _ = DeliveryHandler.MessageRemoved(this, message);

                return;
            }

            List<ChannelClient> clients;
            if (singleClient == null)
                clients = Channel.ClientsClone;
            else
            {
                clients = new List<ChannelClient>();
                clients.Add(singleClient);
            }

            if (clients.Count == 0)
            {
                //if we are queuing, put the message back
                if (onheld)
                    PutMessageBack(message);

                return;
            }

            //create prepared message data
            byte[] messageData = await _writer.Create(message.Message);

            //to all receivers
            foreach (ChannelClient client in clients)
            {
                //to only online receivers
                if (!client.Client.IsConnected)
                    continue;

                //somehow if code comes here (it should not cuz of last "break" in this foreach, break
                if (!message.Message.FirstAcquirer && Options.SendOnlyFirstAcquirer)
                    break;

                //call before send and check decision
                bool canConsumerReceive = await DeliveryHandler.CanConsumerReceive(this, message, client.Client);
                if (!canConsumerReceive)
                    continue;

                //create delivery object
                MessageDelivery delivery = new MessageDelivery(message, client, deadline);
                delivery.FirstAcquirer = message.Message.FirstAcquirer;

                //adds the delivery to time keeper to check timing up
                _timeKeeper.AddAcknowledgeCheck(delivery);

                //send the message
                client.Client.Send(messageData);

                //set as sent, if message is sent to it's first acquirer,
                //set message first acquirer false and re-create byte array data of the message
                bool firstAcquirer = message.Message.FirstAcquirer;

                //mark message is sent
                delivery.MarkAsSent();

                //do after send operations for per message
                _ = DeliveryHandler.ConsumerReceived(this, delivery, client.Client);

                //if we are sending to only first acquirer, break
                if (Options.SendOnlyFirstAcquirer && firstAcquirer)
                    break;

                if (firstAcquirer && clients.Count > 1)
                    messageData = await _writer.Create(message.Message);
            }

            //after all sending operations completed, calls implementation send completed method and complete the operation
            decision = await DeliveryHandler.EndSend(this, message);
            await ApplyDecision(decision, message);

            if (Status != QueueStatus.Route && decision.KeepMessage)
                PutMessageBack(message);
            else
                _ = DeliveryHandler.MessageRemoved(this, message);
        }

        /// <summary>
        /// Adds the message to the queue and pulls first message from the queue.
        /// Usually first message equals message itself.
        /// But sometimes, previous messages might be pending in the queue.
        /// </summary>
        private QueueMessage PullMessage(QueueMessage message)
        {
            QueueMessage held;
            if (message.Message.HighPriority)
            {
                lock (_prefentialMessages)
                {
                    //we don't need push and pull
                    if (_prefentialMessages.Count == 0)
                        return message;

                    _prefentialMessages.AddLast(message);
                    held = _prefentialMessages.First.Value;
                    _prefentialMessages.RemoveFirst();
                }
            }
            else
            {
                lock (_standardMessages)
                {
                    //we don't need push and pull
                    if (_standardMessages.Count == 0)
                        return message;

                    _standardMessages.AddLast(message);
                    held = _standardMessages.First.Value;
                    _standardMessages.RemoveFirst();
                }
            }

            return held;
        }

        /// <summary>
        /// If there is no available receiver when after a message is helded to send to receivers,
        /// This methods puts the message back.
        /// </summary>
        private void PutMessageBack(QueueMessage message)
        {
            if (message.IsFirstQueue)
                message.IsFirstQueue = false;

            if (message.Message.HighPriority)
            {
                lock (_prefentialMessages)
                    _prefentialMessages.AddFirst(message);
            }
            else
            {
                lock (_standardMessages)
                    _standardMessages.AddFirst(message);
            }
        }

        /// <summary>
        /// Applies decision.
        /// If save is chosen, saves the message.
        /// If acknowledge is chosen, sends an ack message to source.
        /// Returns true is allowed
        /// </summary>
        private async Task<bool> ApplyDecision(Decision decision, QueueMessage message)
        {
            if (decision.SaveMessage)
            {
                if (!message.IsSaved)
                    message.IsSaved = await DeliveryHandler.SaveMessage(this, message);
            }

            if (decision.SendAcknowledge == DeliveryAcknowledgeDecision.Always ||
                decision.SendAcknowledge == DeliveryAcknowledgeDecision.IfSaved && message.IsSaved)
            {
                TmqMessage acknowledge = message.Message.CreateAcknowledge();
                await message.Source.SendAsync(acknowledge);
            }

            return decision.Allow;
        }

        /// <summary>
        /// Called when a acknowledge message is received from the client
        /// </summary>
        internal async Task AcknowledgeDelivered(MqClient from, TmqMessage deliveryMessage)
        {
            MessageDelivery delivery = _timeKeeper.FindDelivery(from, deliveryMessage.MessageId);

            if (delivery != null)
            {
                delivery.MarkAsAcknowledged();

                if (delivery.Message.Source != null && delivery.Message.Source.IsConnected)
                {
                    //if client names are hidden, set source as channel name
                    if (Options.HideClientNames)
                        deliveryMessage.Source = null;

                    //target should be channel name, so client can have info where the message comes from
                    deliveryMessage.Target = Channel.Name;

                    delivery.AcknowledgeSentToSource = await delivery.Message.Source.SendAsync(deliveryMessage);
                }
            }

            await DeliveryHandler.AcknowledgeReceived(this, deliveryMessage, delivery);
            ReleaseAcknowledgeLock();
        }

        /// <summary>
        /// If acknowledge lock option is enabled, releases the lock
        /// </summary>
        internal void ReleaseAcknowledgeLock()
        {
            _waitingAcknowledge = false;
        }

        #endregion
    }
}