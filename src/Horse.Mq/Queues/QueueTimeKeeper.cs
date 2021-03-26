using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Mq.Clients;
using Horse.Mq.Delivery;

namespace Horse.Mq.Queues
{
    /// <summary>
    /// Follows all deliveries and their acknowledges, responses and expirations
    /// </summary>
    internal class QueueTimeKeeper
    {
        #region Fields

        /// <summary>
        /// Queue of the keeper
        /// </summary>
        private readonly HorseQueue _queue;

        /// <summary>
        /// Timeout checker timer
        /// </summary>
        private ThreadTimer _timer;

        /// <summary>
        /// All following deliveries
        /// </summary>
        private readonly List<MessageDelivery> _deliveries = new(1024);

        #endregion

        public QueueTimeKeeper(HorseQueue queue)
        {
            _queue = queue;
        }

        #region Methods

        /// <summary>
        /// Runs the queue time keeper timer
        /// </summary>
        public void Run()
        {
            TimeSpan interval = TimeSpan.FromMilliseconds(1000);
            _timer = new ThreadTimer(async () => await Elapse(), interval);
            _timer.Start();
        }

        private async Task Elapse()
        {
            try
            {
                if (!_queue.IsInitialized)
                    return;

                if (_queue.Options.Status != QueueStatus.Broadcast && _queue.Options.MessageTimeout > TimeSpan.Zero)
                    await ProcessReceiveTimeup();

                await ProcessDeliveries();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Clears all following deliveries
        /// </summary>
        public void Reset()
        {
            lock (_deliveries)
                _deliveries.Clear();
        }

        /// <summary>
        /// Destroys the queue time keeper. 
        /// </summary>
        public async Task Destroy()
        {
            Reset();

            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Checks messages if they are not received from any receiver and time is up
        /// Complete the operation about timing up.
        /// </summary>
        private async Task ProcessReceiveTimeup()
        {
            if (!_queue.IsInitialized)
                return;

            List<QueueMessage> temp = new List<QueueMessage>();
            lock (_queue.PriorityMessagesList)
                ProcessReceiveTimeupOnList(_queue.PriorityMessagesList, temp);

            foreach (QueueMessage message in temp)
            {
                _queue.Info.AddMessageTimeout();
                Decision decision = await _queue.DeliveryHandler.MessageTimedOut(_queue, message);
                await _queue.ApplyDecision(decision, message);
                
                foreach (IQueueMessageEventHandler handler in _queue.Server.QueueMessageHandlers)
                    _ = handler.MessageTimedOut(_queue, message);
            }

            temp.Clear();
            lock (_queue.MessagesList)
                ProcessReceiveTimeupOnList(_queue.MessagesList, temp);

            foreach (QueueMessage message in temp)
            {
                _queue.Info.AddMessageTimeout();
                Decision decision = await _queue.DeliveryHandler.MessageTimedOut(_queue, message);
                await _queue.ApplyDecision(decision, message);
                
                foreach (IQueueMessageEventHandler handler in _queue.Server.QueueMessageHandlers)
                    _ = handler.MessageTimedOut(_queue, message);
            }
        }

        /// <summary>
        /// Checks messages in the list and adds them into time up message list and remove from the queue if they are expired.
        /// </summary>
        private void ProcessReceiveTimeupOnList(LinkedList<QueueMessage> list, List<QueueMessage> temp)
        {
            //if the first message has not expired, none of the messages have expired. 
            QueueMessage firstMessage = list.FirstOrDefault();
            if (firstMessage != null && firstMessage.Deadline.HasValue && firstMessage.Deadline > DateTime.UtcNow)
                return;

            foreach (QueueMessage message in list)
            {
                if (!message.Deadline.HasValue)
                    continue;

                if (DateTime.UtcNow > message.Deadline.Value)
                    temp.Add(message);
            }

            foreach (QueueMessage message in temp)
                list.Remove(message);
        }

        /// <summary>
        /// Checks all pending deliveries if they are delivered or time is up
        /// </summary>
        private async Task ProcessDeliveries()
        {
            try
            {
                var rdlist = new List<Tuple<bool, MessageDelivery>>(16);

                lock (_deliveries)
                    foreach (MessageDelivery delivery in _deliveries)
                    {
                        //message acknowledge or came here accidently :)
                        if (delivery.Acknowledge != DeliveryAcknowledge.None || !delivery.AcknowledgeDeadline.HasValue)
                            rdlist.Add(new Tuple<bool, MessageDelivery>(false, delivery));

                        //expired
                        else if (DateTime.UtcNow > delivery.AcknowledgeDeadline.Value)
                            rdlist.Add(new Tuple<bool, MessageDelivery>(true, delivery));

                        //check if all receivers disconnected
                        else if (delivery.Message.CurrentDeliveryReceivers.Count > 0)
                        {
                            bool allDisconnected = delivery.Message.CurrentDeliveryReceivers.All(x => x.Client == null || !x.Client.IsConnected);
                            if (allDisconnected)
                            {
                                rdlist.Add(new Tuple<bool, MessageDelivery>(true, delivery));
                                delivery.Message.CurrentDeliveryReceivers.Clear();
                            }
                        }
                    }

                if (rdlist.Count == 0)
                    return;

                bool released = false;
                foreach (Tuple<bool, MessageDelivery> tuple in rdlist)
                {
                    MessageDelivery delivery = tuple.Item2;
                    if (tuple.Item1)
                    {
                        bool marked = delivery.MarkAsAcknowledgeTimeout();
                        if (!marked)
                        {
                            if (!released)
                            {
                                released = true;
                                _queue.ReleaseAcknowledgeLock(false);
                            }

                            continue;
                        }

                        _queue.Info.AddUnacknowledge();
                        Decision decision = await _queue.DeliveryHandler.AcknowledgeTimedOut(_queue, delivery);

                        if (delivery.Message != null)
                            await _queue.ApplyDecision(decision, delivery.Message);

                        foreach (IQueueMessageEventHandler handler in _queue.Server.QueueMessageHandlers)
                            _ = handler.OnAcknowledgeTimedOut(_queue, delivery);
                        
                        if (!released)
                        {
                            released = true;
                            _queue.ReleaseAcknowledgeLock(false);
                        }
                    }
                }

                IEnumerable<MessageDelivery> rdm = rdlist.Select(x => x.Item2);
                lock (_deliveries)
                    _deliveries.RemoveAll(x => rdm.Contains(x));
            }
            catch (Exception e)
            {
                _queue.Server.SendError("PROCESS_DELIVERIES", e, $"QueueName:{_queue.Name}");
            }
        }

        /// <summary>
        /// Adds acknowledge to check it's timeout
        /// </summary>
        public void AddAcknowledgeCheck(MessageDelivery delivery)
        {
            if (!delivery.Message.Message.WaitResponse || !delivery.AcknowledgeDeadline.HasValue)
                return;

            lock (_deliveries)
                _deliveries.Add(delivery);
        }

        /// <summary>
        /// Finds delivery from message id
        /// </summary>
        public MessageDelivery FindDelivery(MqClient client, string messageId)
        {
            MessageDelivery delivery;

            lock (_deliveries)
                delivery = _deliveries.Find(x => x.Receiver != null
                                                 && x.Receiver.Client.UniqueId == client.UniqueId
                                                 && x.Message.Message.MessageId == messageId);

            return delivery;
        }

        /// <summary>
        /// Finds delivery from message id and removes it from deliveries
        /// </summary>
        public MessageDelivery FindAndRemoveDelivery(MqClient client, string messageId)
        {
            MessageDelivery delivery;

            lock (_deliveries)
            {
                int index = _deliveries.FindIndex(x => x.Receiver != null
                                                       && x.Receiver.Client.UniqueId == client.UniqueId
                                                       && x.Message.Message.MessageId == messageId);

                if (index < 0)
                    return null;

                delivery = _deliveries[index];
                _deliveries.RemoveAt(index);
            }

            return delivery;
        }

        /// <summary>
        /// Removes delivery from being tracked
        /// </summary>
        internal void RemoveDelivery(MessageDelivery delivery)
        {
            lock (_deliveries)
                _deliveries.Remove(delivery);
        }

        /// <summary>
        /// Returns true, if there are pending messages waiting for acknowledge
        /// </summary>
        /// <returns></returns>
        public bool HasPendingDelivery()
        {
            return _deliveries.Count > 0;
        }

        /// <summary>
        /// Returns unique pending message count
        /// </summary>
        public int GetPendingMessageCount()
        {
            int count;
            lock (_deliveries)
                count = _deliveries.Where(x => !string.IsNullOrEmpty(x.Message.Message.MessageId))
                                   .Select(x => x.Message.Message.MessageId)
                                   .Distinct()
                                   .Count();

            return count;
        }

        #endregion
    }
}