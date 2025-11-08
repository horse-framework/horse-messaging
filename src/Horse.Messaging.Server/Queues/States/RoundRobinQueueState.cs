using System;
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

    public RoundRobinQueueState(HorseQueue queue)
    {
        _queue = queue;
    }

    public Task<PullResult> Pull(QueueClient client, HorseMessage request)
    {
        return Task.FromResult(PullResult.StatusNotSupported);
    }

    public async Task<PushResult> Push(QueueMessage message)
    {
        try
        {
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

        //create prepared message data
        byte[] messageData = HorseProtocolWriter.Create(message.Message);

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
        bool sent = await receiver.Client.SendRawAsync(messageData);

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
        int tryCount = 0;
        while (true)
        {
            int index = currentIndex < 0 ? 0 : currentIndex;
            if (index >= _queue.ClientsArray.Length)
            {
                if (!_queue.HasAnyClient())
                    break;

                currentIndex = 0;
                continue;
            }

            for (int i = index; i < _queue.ClientsArray.Length; i++)
            {
                QueueClient client = _queue.ClientsArray[i];

                if (client == null)
                {
                    if (i == 0)
                        break;

                    i = 0;
                    client = _queue.ClientsArray[i];
                    if (client == null)
                        break;
                }

                if (!client.Client.IsConnected)
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

                return new Tuple<QueueClient, int>(client, i + 1);
            }

            tryCount++;
            if (tryCount > 10)
                tryCount = 0;

            if (!_queue.HasAnyClient())
                break;

            //don't try hard so much, wait for next trigger operation of the queue.
            //it will be triggered in 5 secs, anyway
            if (DateTime.UtcNow > retryExpiration)
                break;
        }

        return new Tuple<QueueClient, int>(null, -1);
    }
}