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
    /// Coalesced wake-up for the single drain loop. Consumer availability is checked by scanning,
    /// so multiple ACKs need only one pending signal rather than one permit per historical ACK.
    /// </summary>
    private readonly SemaphoreSlim _clientAvailableSignal = new(0, 1);
    private int _clientAvailableSignalPending;

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
        if (Interlocked.CompareExchange(ref _clientAvailableSignalPending, 1, 0) == 0)
            _clientAvailableSignal.Release();
    }

    public Task<PullResult> Pull(QueueClient client, HorseMessage request)
    {
        return Task.FromResult(PullResult.StatusNotSupported);
    }

    public async Task<PushResult> Push(QueueMessage message)
    {
        // The message lives only in this stack frame until it is either handed to a consumer or put
        // back into the store. GetNextAvailableRRClient can hold it here for up to 30 seconds while
        // the single consumer is busy, and during that time the queue looks empty to auto-destroy.
        // Count it so HorseQueue.IsIdleForDestroy can see it.
        _queue.BeginPush();
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
            _queue.EndPush();
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

        PutBackDecision beginPutBack = message.Decision.PutBack;

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

            deliveryHandler.Tracker.RemoveDelivery(delivery);
            Decision failed = await deliveryHandler.ConsumerReceiveFailed(_queue, delivery, receiver.Client);
            message.Decision = failed;
            bool allowed = await _queue.ApplyDecision(failed, message);

            // A handler that stopped the flow, deleted the message or put it back owns it; the drain goes on.
            // EndSend is not called for a message that was not sent: it committed an unsent message to its
            // producer (CommitWhen.AfterSent), deleted it (memory, no acknowledge) or put a persistent
            // message back a second time.
            if (!allowed || message.IsRemoved || beginPutBack != PutBackDecision.No || failed.PutBack != PutBackDecision.No)
                return PushResult.Success;

            // Otherwise it was lost. It goes back into the queue and the pass ends: the next pass
            // (next push, acknowledge or the trigger timer) picks the next consumer, and a consumer that
            // looks connected but cannot be written to cannot spin the drain loop.
            PushResult restored = _queue.AddMessage(message, false);
            return restored == PushResult.Success ? PushResult.NoConsumers : restored;
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
                // A pending signal is consumed once; historical ACKs cannot cause repeated scans.
                if (await _clientAvailableSignal.WaitAsync(1000))
                {
                    // Reset only after consuming a signal. A timeout can race with an ACK
                    // between its CAS and Release; resetting then would permit an overflow.
                    // ACKs coalesced before this reset are visible to the next client scan.
                    Interlocked.Exchange(ref _clientAvailableSignalPending, 0);
                }
            }

            if (!_queue.HasAnyClient())
                break;

            if (DateTime.UtcNow > retryExpiration)
                break;
        }

        return new Tuple<QueueClient, int>(null, -1);
    }
}
