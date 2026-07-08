using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues.States;

internal class RoundRobinQueueState : IQueueState
{
    public QueueMessage ProcessingMessage { get; private set; }
    public bool TriggerSupported => true;

    private readonly HorseQueue _queue;

    /// <summary>
    /// Round robin client list index
    /// </summary>
    private int _roundRobinIndex = -1;

    /// <summary>
    /// Signal used to wake up GetNextAvailableRRClient when a consumer finishes processing (ACK received).
    /// Replaces the old tight-loop polling with an event-driven wait.
    /// </summary>
    private readonly SemaphoreSlim _clientAvailableSignal = new(0, int.MaxValue);

    public RoundRobinQueueState(HorseQueue queue)
    {
        _queue = queue;
    }

    /// <summary>
    /// Signals that a client has become available (e.g. after ACK).
    /// Wakes up the GetNextAvailableRRClient loop immediately.
    /// </summary>
    internal void SignalClientAvailable()
    {
        try
        {
            _clientAvailableSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already at max, ignore
        }
    }

    public Task<PullResult> Pull(QueueClient client, HorseMessage request)
    {
        return Task.FromResult(PullResult.StatusNotSupported);
    }

    public async Task<PushResult> Push(QueueMessage message)
    {
        try
        {
            if (_queue.Rider.Queue.IsShuttingDown)
                return PushResult.StatusNotSupported;

            if (!message.Deadline.HasValue && _queue.Options.MessageTimeout.Policy != MessageTimeoutPolicy.NoTimeout && _queue.Options.MessageTimeout.MessageDuration > 0)
                message.Deadline = DateTime.UtcNow.AddSeconds(_queue.Options.MessageTimeout.MessageDuration);

            bool waitForAck = _queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge;
            var tuple = await GetNextAvailableRRClient(_roundRobinIndex, message, waitForAck);

            QueueClient cc = tuple.Item1;
            _roundRobinIndex = tuple.Item2;

            if (cc == null)
            {
                PushResult pushResult = _queue.AddMessage(message, false);
                if (pushResult != PushResult.Success)
                    return pushResult;

                return PushResult.NoConsumers;
            }

            ProcessingMessage = message;
            PushResult result = await ProcessMessage(message, cc);

            return result;
        }
        catch (Exception e)
        {
            _queue.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.QueuePush, "RoundRobin Queue Push: " + _queue.Name, e);
            return PushResult.Error;
        }
        finally
        {
            ProcessingMessage = null;
        }
    }

    private async Task<PushResult> ProcessMessage(QueueMessage message, QueueClient receiver)
    {
        //if we need acknowledge from receiver, it has a deadline.
        DateTime? deadline = null;
        if (_queue.Options.Acknowledge != QueueAckDecision.None)
            deadline = DateTime.UtcNow.Add(_queue.Options.AcknowledgeTimeout);

        //return if client unsubsribes while waiting ack of previous message
        if (receiver.Index < 0)
        {
            PushResult pushResult = _queue.AddMessage(message, false);
            if (pushResult != PushResult.Success)
                return pushResult;

            return PushResult.NoConsumers;
        }

        if (message.CurrentDeliveryReceivers.Count > 0)
            message.CurrentDeliveryReceivers.Clear();

        IQueueDeliveryHandler deliveryHandler = _queue.Manager.DeliveryHandler;

        message.Decision = await deliveryHandler.BeginSend(_queue, message);
        if (!await _queue.ApplyDecision(message.Decision, message))
            return PushResult.Success;

        //create delivery object
        MessageDelivery delivery = new MessageDelivery(message, receiver, deadline);

        bool tracked = false;
        while (!tracked)
        {
            tracked = await deliveryHandler.Tracker.Track(delivery);
            if (!tracked)
                await Task.Delay(10);
        }

        if (_queue.Options.Acknowledge != QueueAckDecision.None)
        {
            receiver.CurrentlyProcessing = message;
            receiver.ProcessDeadline = deadline ?? DateTime.UtcNow;
            message.CurrentDeliveryReceivers.Add(receiver);
        }

        //send the message
        bool sent = await receiver.Client.SendAsync(message.Message);

        if (sent)
        {
            receiver.ConsumeCount++;

            //mark message is sent
            delivery.MarkAsSent();
            _queue.Info.AddMessageSend();

            foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
                _ = handler.OnConsumed(_queue, delivery, receiver.Client);
        }
        else
        {
            if (_queue.Options.Acknowledge != QueueAckDecision.None)
            {
                if (receiver.CurrentlyProcessing == message)
                {
                    receiver.ProcessDeadline = DateTime.UtcNow;
                    receiver.CurrentlyProcessing = null;
                    message.CurrentDeliveryReceivers.Remove(receiver);
                }
            }

            message.Decision = await deliveryHandler.ConsumerReceiveFailed(_queue, delivery, receiver.Client);
            deliveryHandler.Tracker.RemoveDelivery(delivery);
        }

        if (!await _queue.ApplyDecision(message.Decision, message))
            return PushResult.Success;

        message.Decision = await deliveryHandler.EndSend(_queue, message);
        await _queue.ApplyDecision(message.Decision, message);

        return PushResult.Success;
    }

    public Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus)
    {
        return Task.FromResult(QueueStatusAction.AllowAndTrigger);
    }

    public Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus)
    {
        return Task.FromResult(QueueStatusAction.Allow);
    }

    /// <summary>
    /// Gets next available client which is not currently consuming any message.
    /// Used for wait for acknowledge situations
    /// </summary>
    private async Task<Tuple<QueueClient, int>> GetNextAvailableRRClient(int currentIndex, QueueMessage message, bool waitForAcknowledge)
    {
        if (!_queue.HasAnyClient())
            return new Tuple<QueueClient, int>(null, -1);

        DateTime retryExpiration = DateTime.UtcNow.AddSeconds(30);
        bool firstScan = true;

        while (true)
        {
            QueueClient[] clients = _queue.ClientsArray;
            int length = clients.Length;
            int index = currentIndex < 0 ? 0 : currentIndex;

            if (index >= length)
                index = 0;

            for (int scan = 0; scan < length; scan++)
            {
                int i = (index + scan) % length;
                QueueClient client = clients[i];

                if (client == null)
                    continue;

                if (!client.Client.IsConnected)
                    continue;
                
                if (client.Blocked)
                    continue;

                if (waitForAcknowledge && client.CurrentlyProcessing != null)
                {
                    if (client.ProcessDeadline < DateTime.UtcNow)
                        client.CurrentlyProcessing = null;
                    else
                        continue;
                }

                var deliveryHandler = _queue.Manager.DeliveryHandler;
                bool canReceive = await deliveryHandler.CanConsumerReceive(_queue, message, client.Client);
                if (!canReceive)
                    continue;

                return new Tuple<QueueClient, int>(client, (i + 1) % length);
            }

            // No available client found after scanning all consumers.
            // On the first scan we allow one immediate retry (the state may have just changed).
            // After that, wait for a signal from AcknowledgeDelivered instead of spinning.
            if (firstScan)
            {
                firstScan = false;
            }
            else
            {
                // Block until a consumer becomes available (ACK received) or timeout (safety fallback).
                // Do NOT drain stale signals before waiting — draining introduces a race where
                // a signal released between the scan and the drain is lost, causing a full
                // timeout delay (up to 1 000 ms) even though the consumer is already free.
                // SemaphoreSlim handles this correctly: if count > 0, WaitAsync returns immediately.
                await _clientAvailableSignal.WaitAsync(1000);
            }

            if (!_queue.HasAnyClient())
                break;

            if (DateTime.UtcNow > retryExpiration)
                break;
        }

        return new Tuple<QueueClient, int>(null, -1);
    }
}
