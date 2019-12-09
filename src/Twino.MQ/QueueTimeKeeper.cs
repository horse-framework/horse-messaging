using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Twino.MQ.Clients;

namespace Twino.MQ
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
        /// Messages with high priority list of the queue
        /// </summary>
        private readonly LinkedList<QueueMessage> _prefentialMessages;

        /// <summary>
        /// Messages list of the queue
        /// </summary>
        private readonly LinkedList<QueueMessage> _standardMessages;

        /// <summary>
        /// Processing timed out messages.
        /// This list is used as temp list.
        /// To prevent re-allocations, defined in here.
        /// </summary>
        private readonly List<QueueMessage> _timeupMessages = new List<QueueMessage>(16);

        /// <summary>
        /// All following deliveries
        /// </summary>
        private readonly List<MessageDelivery> _deliveries = new List<MessageDelivery>(1024);

        /// <summary>
        /// To not lock delivery and ading delivery list, removing deliveries are stored in different list
        /// </summary>
        private readonly List<Tuple<bool, MessageDelivery>> _removingDeliveries = new List<Tuple<bool, MessageDelivery>>(16);

        #endregion

        public QueueTimeKeeper(ChannelQueue queue, LinkedList<QueueMessage> prefentialMessages, LinkedList<QueueMessage> standardMessages)
        {
            _queue = queue;
            _prefentialMessages = prefentialMessages;
            _standardMessages = standardMessages;
        }

        #region Methods

        /// <summary>
        /// Runs the queue time keeper timer
        /// </summary>
        public void Run()
        {
            TimeSpan interval = TimeSpan.FromMilliseconds(1000);
            _timer = new Timer(async s =>
            {
                if (_queue.Options.MessageQueuing && _queue.Options.MessagePendingTimeout > TimeSpan.Zero)
                    await ProcessReceiveTimeup();

                await ProcessDeliveries();
            }, null, interval, interval);
        }

        /// <summary>
        /// Checks messages if they are not received from any receiver and time is up
        /// Complete the operation about timing up.
        /// </summary>
        private async Task ProcessReceiveTimeup()
        {
            _timeupMessages.Clear();
            ProcessReceiveTimeupOnList(_prefentialMessages);

            foreach (QueueMessage message in _timeupMessages)
                await _queue.DeliveryHandler.OnTimeUp(_queue, message);

            _timeupMessages.Clear();
            ProcessReceiveTimeupOnList(_standardMessages);

            foreach (QueueMessage message in _timeupMessages)
                await _queue.DeliveryHandler.OnTimeUp(_queue, message);
        }

        /// <summary>
        /// Checks messages in the list and adds them into time up message list and remove from the queue if they are expired.
        /// </summary>
        private void ProcessReceiveTimeupOnList(LinkedList<QueueMessage> list)
        {
            lock (list)
            {
                foreach (QueueMessage message in list)
                {
                    if (!message.Deadline.HasValue)
                        continue;

                    if (DateTime.UtcNow > message.Deadline.Value)
                        _timeupMessages.Add(message);
                }

                foreach (QueueMessage message in _timeupMessages)
                    list.Remove(message);
            }
        }

        /// <summary>
        /// Checks all pending deliveries if they are delivered or time is up
        /// </summary>
        private async Task ProcessDeliveries()
        {
            var rdlist = new List<Tuple<bool, MessageDelivery>>(16);
            //_removingDeliveries.Clear();

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
                    await _queue.DeliveryHandler.OnAcknowledgeTimeUp(_queue, delivery);
                    if (!released)
                    {
                        released = true;
                        _queue.ReleaseAcknowledgeLock();
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