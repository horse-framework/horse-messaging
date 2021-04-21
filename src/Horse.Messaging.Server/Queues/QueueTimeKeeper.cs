using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues
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
                if (_queue.Status == QueueStatus.NotInitialized)
                    return;

                if (_queue.Options.MessageTimeout > TimeSpan.Zero)
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
            if (_queue.Status == QueueStatus.NotInitialized)
                return;

            List<QueueMessage> temp = new List<QueueMessage>();
            ProcessReceiveTimeupOnList(true, temp);

            foreach (QueueMessage message in temp)
            {
                _queue.Info.AddMessageTimeout();
                Decision decision = await _queue.DeliveryHandler.MessageTimedOut(_queue, message);
                await _queue.ApplyDecision(decision, message);

                foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
                    _ = handler.MessageTimedOut(_queue, message);
            }

            temp.Clear();
            ProcessReceiveTimeupOnList(false, temp);

            foreach (QueueMessage message in temp)
            {
                _queue.Info.AddMessageTimeout();
                Decision decision = await _queue.DeliveryHandler.MessageTimedOut(_queue, message);
                await _queue.ApplyDecision(decision, message);

                foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
                    _ = handler.MessageTimedOut(_queue, message);
            }
        }

        /// <summary>
        /// Checks messages in the list and adds them into time up message list and remove from the queue if they are expired.
        /// </summary>
        private void ProcessReceiveTimeupOnList(bool priority, List<QueueMessage> temp)
        {
            //if the first message has not expired, none of the messages have expired. 
            QueueMessage firstMessage = priority
                ? _queue.Store.GetPriorityNext(false)
                : _queue.Store.GetRegularNext(false);

            if (firstMessage != null && firstMessage.Deadline.HasValue && firstMessage.Deadline > DateTime.UtcNow)
                return;

            Func<QueueMessage, bool> predicate = m => m.Deadline.HasValue && DateTime.UtcNow > m.Deadline.Value;
            temp.AddRange(priority
                              ? _queue.Store.FindAndRemovePriority(predicate)
                              : _queue.Store.FindAndRemoveRegular(predicate));
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

                        foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
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
                _queue.Rider.SendError("PROCESS_DELIVERIES", e, $"QueueName:{_queue.Name}");
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
        public MessageDelivery FindDelivery(MessagingClient client, string messageId)
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
        public MessageDelivery FindAndRemoveDelivery(MessagingClient client, string messageId)
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