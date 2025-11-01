using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Delivery
{
    /// <summary>
    /// Default delivery tracker implementation.
    /// That object tracks sent messages and manages acknowledge and response messages of them.
    /// </summary>
    public class DefaultDeliveryTracker : IDeliveryTracker
    {
        private readonly MessageDelivery[] _deliveries;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private readonly IHorseQueueManager _manager;
        private readonly HorseQueue _queue;
        private Thread _timer;
        private bool _destroyed;

        /// <summary>
        /// Creates new default delivery tracker
        /// </summary>
        public DefaultDeliveryTracker(IHorseQueueManager manager)
        {
            _manager = manager;
            _queue = manager.Queue;

            int deliveryLimit = 128;
            if (_queue.Options.ClientLimit > 0)
                deliveryLimit = Math.Max(1024, _queue.Options.ClientLimit);

            _deliveries = new MessageDelivery[deliveryLimit];
        }

        /// <summary>
        /// Runs the queue time keeper timer
        /// </summary>
        public void Start()
        {
            _timer = new Thread(async () =>
            {
                while (!_destroyed)
                {
                    try
                    {
                        await Task.Delay(1000);

                        if (_queue.Status == QueueStatus.NotInitialized)
                            continue;

                        await ProcessDeliveries();
                    }
                    catch (Exception e)
                    {
                    }
                }
            });

            _timer.IsBackground = true;
            _timer.Priority = ThreadPriority.BelowNormal;
            _timer.Start();
        }

        /// <summary>
        /// Clears all following deliveries
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < _deliveries.Length; i++)
                _deliveries[i] = null;
        }

        /// <summary>
        /// Destroys the queue time keeper. 
        /// </summary>
        public async Task Destroy()
        {
            Reset();
            _destroyed = true;
            await Task.CompletedTask;
        }

        /// <summary>
        /// Returns all actively tracking messages.
        /// The method is thread safe and returns a copy of original collection.
        /// </summary>
        public IEnumerable<QueueMessage> GetDeliveringMessages()
        {
            foreach (MessageDelivery delivery in _deliveries)
            {
                if (delivery == null)
                    yield break;

                yield return delivery.Message;
            }
        }

        /// <summary>
        /// Checks all pending deliveries if they are delivered or time is up
        /// </summary>
        private async Task ProcessDeliveries()
        {
            var ackTimeoutList = new List<MessageDelivery>(16);
            bool atLeastOneRemoved = false;

            await _semaphore.WaitAsync();
            try
            {
                for (int i = 0; i < _deliveries.Length; i++)
                {
                    MessageDelivery delivery = _deliveries[i];
                    if (delivery == null)
                        continue;

                    bool remove = delivery.Acknowledge != DeliveryAcknowledge.None || !delivery.AcknowledgeDeadline.HasValue;

                    if (remove)
                    {
                        _deliveries[i] = null;
                        atLeastOneRemoved = true;
                        continue;
                    }

                    if (DateTime.UtcNow > delivery.AcknowledgeDeadline.Value)
                        remove = true;

                    if (!remove && delivery.Message.CurrentDeliveryReceivers.Count > 0)
                    {
                        bool allDisconnected = delivery.Message.CurrentDeliveryReceivers.All(x => x.Client == null || !x.Client.IsConnected);
                        if (allDisconnected)
                        {
                            remove = true;
                            delivery.Message.CurrentDeliveryReceivers.Clear();
                        }
                    }

                    if (remove)
                    {
                        atLeastOneRemoved = true;
                        ackTimeoutList.Add(delivery);
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }

            foreach (MessageDelivery delivery in ackTimeoutList)
            {
                bool marked = delivery.MarkAsAcknowledgeTimeout();
                if (!marked)
                    continue;

                _queue.Info.AddUnacknowledge();
                Decision decision = await _manager.DeliveryHandler.AcknowledgeTimeout(_queue, delivery);

                HorseMessage ackTimeoutMessage = delivery.Message.Message.CreateAcknowledge("ack-timeout");
                ackTimeoutMessage.ContentType = (ushort)HorseResultCode.RequestTimeout;

                if (delivery.Message != null)
                    _ = _queue.ApplyDecision(decision, delivery.Message, ackTimeoutMessage);

                foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
                    _ = handler.OnAcknowledgeTimedOut(_queue, delivery);

                _queue.MessageUnackEvent.Trigger(new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, delivery.Message.Message.MessageId));
            }

            if (atLeastOneRemoved)
                _queue.ReleaseAcknowledgeLock(false);
        }

        /// <summary>
        /// Adds acknowledge to check it's timeout
        /// </summary>
        public async ValueTask<bool> Track(MessageDelivery delivery)
        {
            if (!delivery.Message.Message.WaitResponse || !delivery.AcknowledgeDeadline.HasValue)
                return true;

            await _semaphore.WaitAsync();
            try
            {
                for (int i = 0; i < _deliveries.Length; i++)
                {
                    if (_deliveries[i] == null)
                    {
                        _deliveries[i] = delivery;
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Finds delivery from message id
        /// </summary>
        public MessageDelivery FindDelivery(MessagingClient client, string messageId, bool remove)
        {
            for (int i = 0; i < _deliveries.Length; i++)
            {
                MessageDelivery delivery = _deliveries[i];

                if (delivery?.Receiver != null && delivery.Receiver.Client.UniqueId == client.UniqueId && delivery.Message.Message.MessageId == messageId)
                {
                    if (remove)
                        _deliveries[i] = null;

                    return delivery;
                }
            }

            return null;
        }

        /// <summary>
        /// Removes delivery from being tracked
        /// </summary>
        public void RemoveDelivery(MessageDelivery delivery)
        {
            for (int i = 0; i < _deliveries.Length; i++)
            {
                MessageDelivery d = _deliveries[i];
                if (d == delivery)
                {
                    _deliveries[i] = null;
                    return;
                }
            }
        }

        /// <summary>
        /// Returns unique pending message count
        /// </summary>
        public int GetDeliveryCount()
        {
            return _deliveries.Count(x => x != null);
        }
    }
}