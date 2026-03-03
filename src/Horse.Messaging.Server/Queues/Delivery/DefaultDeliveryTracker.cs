﻿using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, MessageDelivery> _deliveries = new ConcurrentDictionary<string, MessageDelivery>();

        private readonly IHorseQueueManager _manager;
        private readonly HorseQueue _queue;
        private PeriodicTimer _periodicTimer;
        private CancellationTokenSource _cts;
        private bool _destroyed;

        /// <summary>
        /// Creates new default delivery tracker
        /// </summary>
        public DefaultDeliveryTracker(IHorseQueueManager manager)
        {
            _manager = manager;
            _queue = manager.Queue;
        }

        /// <summary>
        /// Runs the queue time keeper timer
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _ = RunTimerLoop(_cts.Token);
        }

        private async Task RunTimerLoop(CancellationToken cancellationToken)
        {
            Task currentDeliveryTask = null;
            try
            {
                while (await _periodicTimer.WaitForNextTickAsync(cancellationToken))
                {
                    if (_destroyed)
                        break;

                    try
                    {
                        if (_queue.Status == QueueStatus.NotInitialized)
                            continue;

                        if (currentDeliveryTask != null)
                        {
                            if (currentDeliveryTask.IsCompleted || currentDeliveryTask.IsFaulted || currentDeliveryTask.IsCanceled)
                                currentDeliveryTask = null;
                            else
                                continue;
                        }

                        currentDeliveryTask = ProcessDeliveries();
                    }
                    catch
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Clears all following deliveries
        /// </summary>
        public void Reset()
        {
            _deliveries.Clear();
        }

        /// <summary>
        /// Destroys the queue time keeper. 
        /// </summary>
        public async Task Destroy()
        {
            Reset();
            _destroyed = true;
            _cts?.Cancel();
            _periodicTimer?.Dispose();
            _cts?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Returns all actively tracking messages.
        /// The method is thread safe and returns a copy of original collection.
        /// </summary>
        public IEnumerable<QueueMessage> GetDeliveringMessages()
        {
            return _deliveries.Values.Select(x => x.Message).ToList();
        }

        /// <summary>
        /// Checks all pending deliveries if they are delivered or time is up
        /// </summary>
        private async Task ProcessDeliveries()
        {
            List<string> removingDeliveries = new List<string>();
            var ackTimeoutList = new List<MessageDelivery>(16);
            bool atLeastOneRemoved = false;

            foreach (MessageDelivery delivery in _deliveries.Values)
            {
                bool remove = delivery.Acknowledge != DeliveryAcknowledge.None || !delivery.AcknowledgeDeadline.HasValue;

                if (remove)
                {
                    removingDeliveries.Add(delivery.Message.Message.MessageId);
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

            foreach (string deliveryId in removingDeliveries)
                _deliveries.TryRemove(deliveryId, out _);

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
        public ValueTask<bool> Track(MessageDelivery delivery)
        {
            if (!delivery.Message.Message.WaitResponse || !delivery.AcknowledgeDeadline.HasValue)
                return new ValueTask<bool>(true);

            _deliveries.TryAdd(delivery.Message.Message.MessageId, delivery);
            return new ValueTask<bool>(true);
        }

        /// <summary>
        /// Finds delivery from message id
        /// </summary>
        public MessageDelivery FindDelivery(MessagingClient client, string messageId, bool remove)
        {
            if (string.IsNullOrEmpty(messageId))
                return null;

            _deliveries.TryGetValue(messageId, out MessageDelivery delivery);
            if (delivery == null)
                return null;

            if (!IsDeliveryOwnerValid(client, delivery))
                return null;

            if (remove)
                _deliveries.TryRemove(messageId, out _);

            return delivery;
        }

        /// <summary>
        /// Removes delivery from being tracked
        /// </summary>
        public void RemoveDelivery(MessageDelivery delivery)
        {
            _deliveries.TryRemove(delivery.Message.Message.MessageId, out _);
        }

        /// <summary>
        /// Returns unique pending message count
        /// </summary>
        public int GetDeliveryCount()
        {
            return _deliveries.Count;
        }

        private bool IsDeliveryOwnerValid(MessagingClient client, MessageDelivery delivery)
        {
            if (client == null || delivery?.Receiver?.Client == null)
                return true;

            if (delivery.Receiver.Client == client)
                return true;

            // Push queues can have multiple consumers for a single message.
            // Acknowledge from any receiver is accepted.
            return _queue.Type == QueueType.Push;
        }
    }
}
