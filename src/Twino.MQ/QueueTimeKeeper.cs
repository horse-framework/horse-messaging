using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ
{
    internal class QueueTimeKeeper
    {
        private readonly ChannelQueue _queue;
        private Timer _timer;

        private readonly LinkedList<QueueMessage> _prefentialMessages;
        private readonly LinkedList<QueueMessage> _standardMessages;
        private readonly List<QueueMessage> _timeupMessages = new List<QueueMessage>(16);

        private readonly List<MessageDelivery> _addingDeliveries = new List<MessageDelivery>(16);
        private readonly List<MessageDelivery> _deliveries = new List<MessageDelivery>(1024);
        private readonly List<MessageDelivery> _removingDeliveries = new List<MessageDelivery>(16);

        public QueueTimeKeeper(ChannelQueue queue, LinkedList<QueueMessage> prefentialMessages, LinkedList<QueueMessage> standardMessages)
        {
            _queue = queue;
            _prefentialMessages = prefentialMessages;
            _standardMessages = standardMessages;
        }

        /// <summary>
        /// Runs the queue time keeper timer
        /// </summary>
        public void Run()
        {
            TimeSpan interval = TimeSpan.FromMilliseconds(1000);
            _timer = new Timer(async s =>
            {
                if (_queue.Options.MessageQueuing && _queue.Options.ReceiverWaitMaxDuration > TimeSpan.Zero)
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
            //add pending deliveries to add
            lock (_addingDeliveries)
            {
                if (_addingDeliveries.Count > 0)
                {
                    foreach (MessageDelivery delivery in _addingDeliveries)
                        _deliveries.Add(delivery);

                    _addingDeliveries.Clear();
                }
            }

            _removingDeliveries.Clear();

            foreach (MessageDelivery delivery in _deliveries)
            {
                //message delivered or came here accidently :)
                if (delivery.IsDelivered || !delivery.DeliveryDeadline.HasValue)
                {
                    _removingDeliveries.Add(delivery);
                    continue;
                }

                //expired
                if (DateTime.UtcNow > delivery.DeliveryDeadline.Value)
                {
                    _removingDeliveries.Add(delivery);
                    delivery.MarkAsDeliveryTimedUp();
                    await _queue.DeliveryHandler.OnDeliveryTimeUp(_queue, delivery);
                }
            }

            _deliveries.RemoveAll(x => _removingDeliveries.Contains(x));
        }

        /// <summary>
        /// Adds delivery to check it's timeout
        /// </summary>
        public void AddDeliveryCheck(MessageDelivery delivery)
        {
            if (!delivery.DeliveryDeadline.HasValue)
                return;

            lock (_addingDeliveries)
                _addingDeliveries.Add(delivery);
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
        
    }
}