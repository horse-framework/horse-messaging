using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
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
        /// Default TMQ Writer class for the queue
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

        /// <summary>
        /// Time keeper for the queue.
        /// Checks message receiver deadlines and delivery deadlines.
        /// </summary>
        private readonly QueueTimeKeeper _timeKeeper;

        /// <summary>
        /// Wait acknowledge cross thread locker
        /// </summary>
        private SemaphoreSlim _semaphore;

        /// <summary>
        /// This task holds the code until acknowledge is received
        /// </summary>
        private TaskCompletionSource<bool> _acknowledgeCallback;

        /// <summary>
        /// Round robin client list index
        /// </summary>
        private int _roundRobinIndex = -1;

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

            _timeKeeper = new QueueTimeKeeper(this);
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

        #endregion

        #region Fill

        /// <summary>
        /// Fills JSON object data to the queue
        /// </summary>
        public async Task FillJson<T>(IEnumerable<T> items, bool createAsSaved, bool highPriority) where T : class
        {
            foreach (T item in items)
            {
                TmqMessage message = new TmqMessage(MessageType.Channel, Channel.Name);
                message.FirstAcquirer = true;
                message.HighPriority = highPriority;
                message.AcknowledgeRequired = Options.RequestAcknowledge;
                message.ContentType = Id;

                if (Options.UseMessageId)
                    message.SetMessageId(Channel.Server.MessageIdGenerator.Create());

                await message.SetJsonContent(item);
                
                QueueMessage qm = new QueueMessage(message, createAsSaved);

                if (highPriority)
                    lock (HighPriorityLinkedList)
                        HighPriorityLinkedList.AddLast(qm);
                else
                    lock (RegularLinkedList)
                        RegularLinkedList.AddLast(qm);
            }
        }

        /// <summary>
        /// Fills JSON object data to the queue.
        /// Creates new TmqMessage and before writing content and adding into queue calls the action.
        /// </summary>
        public async Task FillJson<T>(IEnumerable<T> items, bool createAsSaved, Action<TmqMessage, T> action) where T : class
        {
            foreach (T item in items)
            {
                TmqMessage message = new TmqMessage(MessageType.Channel, Channel.Name);
                message.FirstAcquirer = true;
                message.AcknowledgeRequired = Options.RequestAcknowledge;
                message.ContentType = Id;

                if (Options.UseMessageId)
                    message.SetMessageId(Channel.Server.MessageIdGenerator.Create());

                action(message, item);
                await message.SetJsonContent(item);

                QueueMessage qm = new QueueMessage(message, createAsSaved);

                if (message.HighPriority)
                    lock (HighPriorityLinkedList)
                        HighPriorityLinkedList.AddLast(qm);
                else
                    lock (RegularLinkedList)
                        RegularLinkedList.AddLast(qm);
            }
        }

        /// <summary>
        /// Fills string data to the queue
        /// </summary>
        public void FillString(IEnumerable<string> items, bool createAsSaved, bool highPriority)
        {
            foreach (string item in items)
            {
                TmqMessage message = new TmqMessage(MessageType.Channel, Channel.Name);
                message.FirstAcquirer = true;
                message.HighPriority = highPriority;
                message.AcknowledgeRequired = Options.RequestAcknowledge;
                message.ContentType = Id;

                if (Options.UseMessageId)
                    message.SetMessageId(Channel.Server.MessageIdGenerator.Create());

                message.Content = new MemoryStream(Encoding.UTF8.GetBytes(item));
                message.Content.Position = 0;
                message.CalculateLengths();

                QueueMessage qm = new QueueMessage(message, createAsSaved);

                if (highPriority)
                    lock (HighPriorityLinkedList)
                        HighPriorityLinkedList.AddLast(qm);
                else
                    lock (RegularLinkedList)
                        RegularLinkedList.AddLast(qm);
            }
        }

        /// <summary>
        /// Fills binary data to the queue
        /// </summary>
        public void FillData(IEnumerable<byte[]> items, bool createAsSaved, bool highPriority)
        {
            foreach (byte[] item in items)
            {
                TmqMessage message = new TmqMessage(MessageType.Channel, Channel.Name);
                message.FirstAcquirer = true;
                message.HighPriority = highPriority;
                message.AcknowledgeRequired = Options.RequestAcknowledge;
                message.ContentType = Id;

                if (Options.UseMessageId)
                    message.SetMessageId(Channel.Server.MessageIdGenerator.Create());

                message.Content = new MemoryStream(item);
                message.Content.Position = 0;
                message.CalculateLengths();

                QueueMessage qm = new QueueMessage(message, createAsSaved);

                if (highPriority)
                    lock (HighPriorityLinkedList)
                        HighPriorityLinkedList.AddLast(qm);
                else
                    lock (RegularLinkedList)
                        RegularLinkedList.AddLast(qm);
            }
        }

        /// <summary>
        /// Fills TMQ Message objects to the queue
        /// </summary>
        public void FillMessage(IEnumerable<TmqMessage> messages, bool isSaved)
        {
            foreach (TmqMessage message in messages)
            {
                message.SetTarget(Channel.Name);
                message.ContentType = Id;

                if (Options.UseMessageId && string.IsNullOrEmpty(message.MessageId))
                    message.SetMessageId(Channel.Server.MessageIdGenerator.Create());

                message.CalculateLengths();

                QueueMessage qm = new QueueMessage(message, isSaved);

                if (message.HighPriority)
                    lock (HighPriorityLinkedList)
                        HighPriorityLinkedList.AddLast(qm);
                else
                    lock (RegularLinkedList)
                        RegularLinkedList.AddLast(qm);
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

                _timeKeeper.Reset();
            }

            Status = status;

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
        /// Client pulls a message from the queue
        /// </summary>
        internal async Task Pull(ChannelClient client, TmqMessage request)
        {
            if (Status != QueueStatus.Pull)
                return;

            QueueMessage message = null;

            //pull from prefential messages
            if (HighPriorityLinkedList.Count > 0)
                lock (HighPriorityLinkedList)
                {
                    message = HighPriorityLinkedList.First.Value;
                    HighPriorityLinkedList.RemoveFirst();

                    if (message != null)
                        message.IsInQueue = false;
                }

            //if there is no prefential message, pull from standard messages
            if (message == null && RegularLinkedList.Count > 0)
            {
                lock (RegularLinkedList)

                {
                    message = RegularLinkedList.First.Value;
                    RegularLinkedList.RemoveFirst();

                    if (message != null)
                        message.IsInQueue = false;
                }
            }

            //there is no pullable message
            if (message == null)
            {
                await client.Client.SendAsync(MessageBuilder.ResponseStatus(request, KnownContentTypes.NotFound));
                return;
            }

            try
            {
                await ProcessPullMessage(client, request, message);
            }
            catch (Exception ex)
            {
                Info.AddError();
                try
                {
                    Decision decision = await DeliveryHandler.ExceptionThrown(this, message, ex);
                    await ApplyDecision(decision, message);

                    if (decision.KeepMessage && !message.IsInQueue)
                        PutMessageBack(message);
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
                message.Message.SetSource(null);

            //process the message
            QueueMessage held = null;
            try
            {
                //fire message receive event
                Info.AddMessageReceive();
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
                    message.Message.SetMessageId(Channel.Server.MessageIdGenerator.Create());

                switch (Status)
                {
                    //just send the message to receivers
                    case QueueStatus.Route:
                        held = message;
                        await ProcessMessage(message);
                        break;

                    //keep the message in queue send send it to receivers
                    //if there is no receiver, message will kept back in the queue
                    case QueueStatus.Push:
                        held = PullMessage(message);
                        await ProcessMessage(held);
                        break;

                    //redirects message to consumers with round robin algorithm
                    case QueueStatus.RoundRobin:
                        held = PullMessage(message);
                        ChannelClient cc = Channel.GetNextRRClient(ref _roundRobinIndex);
                        if (cc != null)
                            await ProcessMessage(held, cc);
                        else
                            PutMessageBack(held);
                        break;

                    //dont send the message, just put it to queue
                    case QueueStatus.Pull:
                    case QueueStatus.Paused:
                        if (message.Message.HighPriority)
                            lock (HighPriorityLinkedList)
                                HighPriorityLinkedList.AddLast(message);
                        else
                            lock (RegularLinkedList)
                                RegularLinkedList.AddLast(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                Info.AddError();
                try
                {
                    Decision decision = await DeliveryHandler.ExceptionThrown(this, held, ex);
                    if (held != null)
                    {
                        await ApplyDecision(decision, held);

                        if (decision.KeepMessage && !held.IsInQueue)
                            PutMessageBack(held);
                    }
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
            if (Status == QueueStatus.Push || Status == QueueStatus.RoundRobin)
            {
                if (HighPriorityLinkedList.Count > 0)
                    await ProcessPendingMessages(HighPriorityLinkedList);

                if (RegularLinkedList.Count > 0)
                    await ProcessPendingMessages(RegularLinkedList);
            }

            _triggering = false;
        }

        /// <summary>
        /// Start to process all pending messages.
        /// This method is called after a client is subscribed to the queue.
        /// </summary>
        private async Task ProcessPendingMessages(LinkedList<QueueMessage> list)
        {
            int max = list.Count;
            for (int i = 0; i < max; i++)
            {
                QueueMessage message;
                lock (list)
                {
                    if (list.Count == 0)
                        return;

                    message = list.First.Value;
                    list.RemoveFirst();
                    message.IsInQueue = false;
                }

                try
                {
                    await ProcessMessage(message);
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

        /// <summary>
        /// Searches receivers of the message and process the send operation
        /// </summary>
        private async Task ProcessMessage(QueueMessage message, ChannelClient singleClient = null)
        {
            //if we need acknowledge, we are sending this information to receivers that we require response
            message.Message.AcknowledgeRequired = Options.RequestAcknowledge;

            //if we need acknowledge from receiver, it has a deadline.
            DateTime? deadline = null;
            if (Options.RequestAcknowledge)
                deadline = DateTime.UtcNow.Add(Options.AcknowledgeTimeout);

            //find receivers. if single client assigned, create one-element list
            List<ChannelClient> clients;
            if (singleClient == null)
                clients = Channel.ClientsClone;
            else
            {
                clients = new List<ChannelClient>();
                clients.Add(singleClient);
            }

            //if there are not receivers, complete send operation
            if (clients.Count == 0)
            {
                if (Status == QueueStatus.Push || Status == QueueStatus.RoundRobin)
                    PutMessageBack(message);
                else
                {
                    Info.AddMessageRemove();
                    _ = DeliveryHandler.MessageRemoved(this, message);
                }

                return;
            }

            //if to process next message is requires previous message acknowledge, wait here
            if (Options.RequestAcknowledge && Options.WaitForAcknowledge)
                await WaitForAcknowledge(message);

            message.Decision = await DeliveryHandler.BeginSend(this, message);
            if (!await ApplyDecision(message.Decision, message))
                return;

            //create prepared message data
            byte[] messageData = await _writer.Create(message.Message);

            Decision final = new Decision(false, false, false, DeliveryAcknowledgeDecision.None);
            bool messageIsSent = false;

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
                Decision ccrd = await DeliveryHandler.CanConsumerReceive(this, message, client.Client);
                final = CreateFinalDecision(final, ccrd);

                if (!ccrd.Allow)
                    continue;

                //create delivery object
                MessageDelivery delivery = new MessageDelivery(message, client, deadline);
                delivery.FirstAcquirer = message.Message.FirstAcquirer;

                //send the message
                bool sent = client.Client.Send(messageData);

                if (sent)
                {
                    messageIsSent = true;

                    //adds the delivery to time keeper to check timing up
                    _timeKeeper.AddAcknowledgeCheck(delivery);

                    //set as sent, if message is sent to it's first acquirer,
                    //set message first acquirer false and re-create byte array data of the message
                    bool firstAcquirer = message.Message.FirstAcquirer;

                    //mark message is sent
                    delivery.MarkAsSent();

                    //do after send operations for per message
                    Info.AddConsumerReceive();
                    Decision d = await DeliveryHandler.ConsumerReceived(this, delivery, client.Client);
                    final = CreateFinalDecision(final, d);

                    //if we are sending to only first acquirer, break
                    if (Options.SendOnlyFirstAcquirer && firstAcquirer)
                        break;

                    if (firstAcquirer && clients.Count > 1)
                        messageData = await _writer.Create(message.Message);
                }
                else
                {
                    Decision d = await DeliveryHandler.ConsumerReceiveFailed(this, delivery, client.Client);
                    final = CreateFinalDecision(final, d);
                }
            }

            message.Decision = final;
            if (!await ApplyDecision(final, message))
                return;

            //after all sending operations completed, calls implementation send completed method and complete the operation
            if (messageIsSent)
                Info.AddMessageSend();

            message.Decision = await DeliveryHandler.EndSend(this, message);
            await ApplyDecision(message.Decision, message);

            if (message.Decision.Allow && !message.Decision.KeepMessage)
            {
                Info.AddMessageRemove();
                _ = DeliveryHandler.MessageRemoved(this, message);
            }
        }

        /// <summary>
        /// Creates final decision from multiple decisions.
        /// Final decision has bests choices for each decision.
        /// </summary>
        private static Decision CreateFinalDecision(Decision final, Decision decision)
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
        /// Adds the message to the queue and pulls first message from the queue.
        /// Usually first message equals message itself.
        /// But sometimes, previous messages might be pending in the queue.
        /// </summary>
        private QueueMessage PullMessage(QueueMessage message)
        {
            QueueMessage held;
            if (message.Message.HighPriority)
            {
                lock (HighPriorityLinkedList)
                {
                    //we don't need push and pull
                    if (HighPriorityLinkedList.Count == 0)
                    {
                        message.IsInQueue = false;
                        return message;
                    }

                    HighPriorityLinkedList.AddLast(message);
                    message.IsInQueue = true;
                    held = HighPriorityLinkedList.First.Value;
                    HighPriorityLinkedList.RemoveFirst();
                }
            }
            else
            {
                lock (RegularLinkedList)
                {
                    //we don't need push and pull
                    if (RegularLinkedList.Count == 0)
                    {
                        message.IsInQueue = false;
                        return message;
                    }

                    RegularLinkedList.AddLast(message);
                    message.IsInQueue = true;
                    held = RegularLinkedList.First.Value;
                    RegularLinkedList.RemoveFirst();
                }
            }

            if (held != null)
                held.IsInQueue = false;

            return held;
        }

        /// <summary>
        /// Process pull request and sends queue message to requester as response
        /// </summary>
        private async Task ProcessPullMessage(ChannelClient requester, TmqMessage request, QueueMessage message)
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

            message.Decision = await DeliveryHandler.BeginSend(this, message);
            if (!await ApplyDecision(message.Decision, message))
                return;

            //if message is sent before and this is second client, skip the process
            bool skip = !message.Message.FirstAcquirer && Options.SendOnlyFirstAcquirer;
            if (skip)
            {
                if (!message.Decision.KeepMessage)
                {
                    Info.AddMessageRemove();
                    _ = DeliveryHandler.MessageRemoved(this, message);
                }

                return;
            }

            //call before send and check decision
            message.Decision = await DeliveryHandler.CanConsumerReceive(this, message, requester.Client);
            if (!await ApplyDecision(message.Decision, message))
                return;

            //create delivery object
            MessageDelivery delivery = new MessageDelivery(message, requester, deadline);
            delivery.FirstAcquirer = message.Message.FirstAcquirer;

            //change to response message, send, change back to channel message
            string mid = message.Message.MessageId;
            message.Message.SetMessageId(request.MessageId);
            message.Message.Type = MessageType.Response;

            bool sent = requester.Client.Send(message.Message);
            message.Message.SetMessageId(mid);
            message.Message.Type = MessageType.Channel;

            if (sent)
            {
                _timeKeeper.AddAcknowledgeCheck(delivery);
                delivery.MarkAsSent();

                //do after send operations for per message
                Info.AddConsumerReceive();
                message.Decision = await DeliveryHandler.ConsumerReceived(this, delivery, requester.Client);

                //after all sending operations completed, calls implementation send completed method and complete the operation
                Info.AddMessageSend();

                if (!await ApplyDecision(message.Decision, message))
                    return;
            }
            else
            {
                message.Decision = await DeliveryHandler.ConsumerReceiveFailed(this, delivery, requester.Client);
                if (!await ApplyDecision(message.Decision, message))
                    return;
            }

            message.Decision = await DeliveryHandler.EndSend(this, message);
            await ApplyDecision(message.Decision, message);

            if (message.Decision.Allow && !message.Decision.KeepMessage)
            {
                Info.AddMessageRemove();
                _ = DeliveryHandler.MessageRemoved(this, message);
            }
        }

        /// <summary>
        /// If there is no available receiver when after a message is helded to send to receivers,
        /// This methods puts the message back.
        /// </summary>
        private void PutMessageBack(QueueMessage message)
        {
            if (message.IsFirstQueue)
                message.IsFirstQueue = false;

            if (message.IsInQueue)
                return;

            if (message.Message.HighPriority)
            {
                lock (HighPriorityLinkedList)
                    HighPriorityLinkedList.AddFirst(message);
            }

            else
            {
                lock (RegularLinkedList)
                    RegularLinkedList.AddFirst(message);
            }

            message.IsInQueue = true;
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
                PutMessageBack(message);

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
        private async Task WaitForAcknowledge(QueueMessage message)
        {
            //if we will lock the queue until ack received, we must request ack
            if (!message.Message.AcknowledgeRequired)
                message.Message.AcknowledgeRequired = true;

            if (_acknowledgeCallback == null)
                return;

            //lock the object, because pending ack message should be queued
            if (_semaphore == null)
                _semaphore = new SemaphoreSlim(1, 1024);

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
            MessageDelivery delivery = _timeKeeper.FindDelivery(from, deliveryMessage.MessageId);

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