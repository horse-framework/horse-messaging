using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;

namespace Twino.MQ.Queues
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
        private readonly ChannelQueue _queue;

        /// <summary>
        /// Timeout checker timer
        /// </summary>
        private Timer _timer;

        /// <summary>
        /// All following deliveries
        /// </summary>
        private readonly List<MessageDelivery> _deliveries = new List<MessageDelivery>(1024);

        #endregion

        public QueueTimeKeeper(ChannelQueue queue)
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
            _timer = new Timer(s => _ = Elapse(), null, interval, interval);
        }

        private async Task Elapse()
        {
            if (_queue.Options.Status != QueueStatus.Route && _queue.Options.MessageTimeout > TimeSpan.Zero)
                await ProcessReceiveTimeup();

            await ProcessDeliveries();
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
                await _timer.DisposeAsync();
        }

        /// <summary>
        /// Checks messages if they are not received from any receiver and time is up
        /// Complete the operation about timing up.
        /// </summary>
        private async Task ProcessReceiveTimeup()
        {
            List<QueueMessage> temp = new List<QueueMessage>();
            lock (_queue.HighPriorityLinkedList)
                ProcessReceiveTimeupOnList(_queue.HighPriorityLinkedList, temp);

            foreach (QueueMessage message in temp)
            {
                _queue.Info.AddMessageTimeout();
                Decision decision = await _queue.DeliveryHandler.MessageTimedOut(_queue, message);
                await _queue.ApplyDecision(decision, message);
            }

            temp.Clear();
            lock (_queue.RegularLinkedList)
                ProcessReceiveTimeupOnList(_queue.RegularLinkedList, temp);

            foreach (QueueMessage message in temp)
            {
                _queue.Info.AddMessageTimeout();
                Decision decision = await _queue.DeliveryHandler.MessageTimedOut(_queue, message);
                await _queue.ApplyDecision(decision, message);
            }
        }

        /// <summary>
        /// Checks messages in the list and adds them into time up message list and remove from the queue if they are expired.
        /// </summary>
        private void ProcessReceiveTimeupOnList(LinkedList<QueueMessage> list, List<QueueMessage> temp)
        {
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
            var rdlist = new List<Tuple<bool, MessageDelivery>>(16);

            lock (_deliveries)
                foreach (MessageDelivery delivery in _deliveries)
                {
                    //message acknowledge or came here accidently :)
                    if (delivery.IsAcknowledged || !delivery.AcknowledgeDeadline.HasValue)
                        rdlist.Add(new Tuple<bool, MessageDelivery>(false, delivery));

                    //expired
                    else if (DateTime.UtcNow > delivery.AcknowledgeDeadline.Value)
                        rdlist.Add(new Tuple<bool, MessageDelivery>(true, delivery));
                }

            if (rdlist.Count == 0)
                return;

            bool released = false;
            foreach (Tuple<bool, MessageDelivery> tuple in rdlist)
            {
                MessageDelivery delivery = tuple.Item2;
                if (tuple.Item1)
                {
                    delivery.MarkAsAcknowledgeTimedUp();
                    _queue.Info.AddUnacknowledge();
                    Decision decision = await _queue.DeliveryHandler.AcknowledgeTimedOut(_queue, delivery);

                    if (delivery.Message != null)
                        await _queue.ApplyDecision(decision, delivery.Message);

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

        /// <summary>
        /// Adds acknowledge to check it's timeout
        /// </summary>
        public void AddAcknowledgeCheck(MessageDelivery delivery)
        {
            if (!delivery.AcknowledgeDeadline.HasValue)
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

        #endregion
    }
}