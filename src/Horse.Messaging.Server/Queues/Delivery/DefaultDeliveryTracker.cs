using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Delivery;

/// <summary>
/// Default delivery tracker implementation.
/// That object tracks sent messages and manages acknowledge and response messages of them.
/// </summary>
public class DefaultDeliveryTracker : IDeliveryTracker
{
    private readonly ConcurrentDictionary<string, MessageDelivery> _deliveries = new ConcurrentDictionary<string, MessageDelivery>();

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
                    _queue.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.QueueDeliveryProcess, "Process Deliveries of Queue: " + _queue.Name, e);
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
        _deliveries.Clear();
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
    public List<QueueMessage> GetDeliveringMessages()
    {
        List<QueueMessage> messages;
        messages = _deliveries.Values.Select(x => x.Message).ToList();
        return messages;
    }

    /// <summary>
    /// Checks all pending deliveries if they are delivered or time is up
    /// </summary>
    private async Task ProcessDeliveries()
    {
        var rdlist = new List<Tuple<bool, MessageDelivery>>(16);

        foreach (var pair in _deliveries)
        {
            var delivery = pair.Value;
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

        foreach (Tuple<bool, MessageDelivery> tuple in rdlist)
        {
            MessageDelivery delivery = tuple.Item2;
            if (tuple.Item1)
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

            string messageId = tuple.Item2?.Message?.Message?.MessageId;

            if (!string.IsNullOrEmpty(messageId))
                _deliveries.TryRemove(messageId, out _);
        }

        if (rdlist.Count > 0)
            _queue.ReleaseAcknowledgeLock(false);
    }

    /// <summary>
    /// Adds acknowledge to check it's timeout
    /// </summary>
    public void Track(MessageDelivery delivery)
    {
        if (!delivery.Message.Message.WaitResponse || !delivery.AcknowledgeDeadline.HasValue)
            return;

        _deliveries.TryAdd(delivery.Message.Message.MessageId, delivery);
    }

    /// <summary>
    /// Finds delivery from message id
    /// </summary>
    public MessageDelivery FindDelivery(MessagingClient client, string messageId)
    {
        _deliveries.TryGetValue(messageId, out MessageDelivery delivery);
        return delivery;
    }

    /// <summary>
    /// Finds delivery from message id and removes it from deliveries
    /// </summary>
    public MessageDelivery FindAndRemoveDelivery(MessagingClient client, string messageId)
    {
        _deliveries.TryRemove(messageId, out MessageDelivery delivery);
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
}