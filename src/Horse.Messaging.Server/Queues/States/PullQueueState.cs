using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues.States;

internal enum ClearDecision
{
    None,
    All,
    Priority,
    Regular
}

internal class PullQueueState : IQueueState
{
    public QueueMessage ProcessingMessage { get; private set; }
    public bool TriggerSupported => false;

    private readonly HorseQueue _queue;

    public PullQueueState(HorseQueue queue)
    {
        _queue = queue;
    }

    /// <summary>
    /// Reads Count header value and returns as integer
    /// </summary>
    private static int FindCount(HorseMessage request)
    {
        string countStr = request.FindHeader(HorseHeaders.COUNT);

        if (string.IsNullOrEmpty(countStr))
            return 0;

        return Convert.ToInt32(countStr.Trim());
    }

    /// <summary>
    /// Reads Clear header value and returns as enum
    /// </summary>
    private static ClearDecision FindClearDecision(HorseMessage request)
    {
        string clearStr = request.FindHeader(HorseHeaders.CLEAR);

        if (string.IsNullOrEmpty(clearStr))
            return ClearDecision.None;

        clearStr = clearStr.Trim();

        if (clearStr.Equals("all", StringComparison.OrdinalIgnoreCase))
            return ClearDecision.All;

        if (clearStr.Equals("high-priority", StringComparison.OrdinalIgnoreCase))
            return ClearDecision.Priority;

        if (clearStr.Equals("default-priority", StringComparison.OrdinalIgnoreCase))
            return ClearDecision.Regular;

        return ClearDecision.None;
    }

    /// <summary>
    /// Reads Info header value and returns as boolean
    /// </summary>
    private static bool FindInfoRequest(HorseMessage request)
    {
        string infoStr = request.FindHeader(HorseHeaders.INFO);
        return !string.IsNullOrEmpty(infoStr) && infoStr.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads Order header and returns true if LIFO is requested
    /// </summary>
    private static bool FindLifoOrder(HorseMessage request)
    {
        string orderStr = request.FindHeader(HorseHeaders.ORDER);
        return !string.IsNullOrEmpty(orderStr) && orderStr.Trim().Equals(HorseHeaders.LIFO, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<PullResult> Pull(QueueClient client, HorseMessage request)
    {
        if (_queue.Rider.Queue.IsShuttingDown)
            return PullResult.StatusNotSupported;

        if (client.Blocked)
        {
            await client.Client.SendAsync(MessageBuilder.CreateNoContentPullResponse(request, HorseHeaders.UNACCEPTABLE));
            return PullResult.Unacceptable;
        }

        int index = 1;
        int count = FindCount(request);
        if (count < 1)
        {
            await client.Client.SendAsync(MessageBuilder.CreateNoContentPullResponse(request, HorseHeaders.UNACCEPTABLE));
            return PullResult.Unacceptable;
        }

        if (_queue.Status == QueueStatus.NotInitialized)
        {
            await client.Client.SendAsync(MessageBuilder.CreateNoContentPullResponse(request, HorseHeaders.EMPTY));
            return PullResult.Empty;
        }

        int msgCount = _queue.Manager.MessageStore.Count();
        if (msgCount == 0)
            msgCount += _queue.Manager.PriorityMessageStore.Count();

        if (msgCount == 0)
        {
            await client.Client.SendAsync(MessageBuilder.CreateNoContentPullResponse(request, HorseHeaders.EMPTY));
            return PullResult.Empty;
        }

        ClearDecision clear = FindClearDecision(request);
        bool sendInfo = FindInfoRequest(request);
        bool lifo = FindLifoOrder(request);

        if (_queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge)
            await _queue.WaitForAcknowledge();

        if (_queue.Rider.Cluster.Options.Mode == ClusterMode.Reliable)
            try
            {
                await _queue.QueueLock.WaitAsync();
            }
            finally
            {
                _queue.QueueLock.Release();
            }

        Tuple<QueueMessage, int, int> messageTuple = DequeueMessage(sendInfo, count == index ? clear : ClearDecision.None, lifo);

        //there is no pullable message
        if (messageTuple.Item1 == null)
        {
            await client.Client.SendAsync(MessageBuilder.CreateNoContentPullResponse(request, HorseHeaders.EMPTY));
            return PullResult.Empty;
        }

        List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();
        KeyValuePair<string, string> requestId = new KeyValuePair<string, string>(HorseHeaders.REQUEST_ID, request.MessageId);
        KeyValuePair<string, string> countHeader = new KeyValuePair<string, string>(HorseHeaders.COUNT, count.ToString());

        while (index <= count)
        {
            QueueMessage message = messageTuple.Item1;
            if (message == null)
                break;

            try
            {
                headers.Add(requestId);
                headers.Add(countHeader);
                headers.Add(new KeyValuePair<string, string>(HorseHeaders.INDEX, index.ToString()));

                if (sendInfo)
                {
                    headers.Add(new KeyValuePair<string, string>(HorseHeaders.PRIORITY_MESSAGES, messageTuple.Item2.ToString()));
                    headers.Add(new KeyValuePair<string, string>(HorseHeaders.MESSAGES, messageTuple.Item3.ToString()));
                }

                bool processed = await ProcessPull(client, request, message, headers);
                if (!processed)
                    break;

                index++;
                if (index > count)
                    break;

                messageTuple = DequeueMessage(sendInfo, count == index ? clear : ClearDecision.None, lifo);
                headers.Clear();
            }
            catch (Exception ex)
            {
                _queue.Info.AddError();
                _queue.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.QueuePull, "Pull Queue: " + _queue.Name, ex);

                await client.Client.SendAsync(MessageBuilder.CreateNoContentPullResponse(request, HorseHeaders.ERROR));
                return PullResult.Unacceptable;
            }
            finally
            {
                ProcessingMessage = null;
            }
        }

        await client.Client.SendAsync(MessageBuilder.CreateNoContentPullResponse(request, HorseHeaders.END));
        return PullResult.Success;
    }

    /// <summary>
    /// Finds and dequeues a message from the queue.
    /// If there is not message, returns null
    /// </summary>
    private Tuple<QueueMessage, int, int> DequeueMessage(bool getInfo, ClearDecision clear, bool lifo)
    {
        int prioMessageCount = 0;
        int messageCount = 0;

        QueueMessage message;

        if (lifo)
        {
            message = _queue.Manager.PriorityMessageStore.ConsumeLast();
            if (message == null)
                message = _queue.Manager.MessageStore.ConsumeLast();
        }
        else
        {
            message = _queue.Manager.PriorityMessageStore.ConsumeFirst();
            if (message == null)
                message = _queue.Manager.MessageStore.ConsumeFirst();
        }

        if (getInfo)
        {
            prioMessageCount = _queue.Manager.PriorityMessageStore.Count();
            messageCount = _queue.Manager.MessageStore.Count();
        }

        if (clear == ClearDecision.All || clear == ClearDecision.Priority)
            _queue.ClearStore(_queue.Manager.PriorityMessageStore);

        if (clear == ClearDecision.All || clear == ClearDecision.Regular)
            _queue.ClearStore(_queue.Manager.MessageStore);

        if (message != null)
            ProcessingMessage = message;

        return new Tuple<QueueMessage, int, int>(message, prioMessageCount, messageCount);
    }

    /// <summary>
    /// Process pull request and sends queue message to requester as response
    /// </summary>
    private async Task<bool> ProcessPull(QueueClient requester, HorseMessage request, QueueMessage message, IList<KeyValuePair<string, string>> headers)
    {
        if (requester.Blocked)
        {
            RestoreUndeliveredMessage(message);
            return false;
        }

        //if we need acknowledge, we are sending this information to receivers that we require response
        message.Message.WaitResponse = _queue.Options.Acknowledge != QueueAckDecision.None;

        //if we need acknowledge from receiver, it has a deadline.
        DateTime? deadline = null;
        if (_queue.Options.Acknowledge != QueueAckDecision.None)
            deadline = DateTime.UtcNow.Add(_queue.Options.AcknowledgeTimeout);

        if (message.CurrentDeliveryReceivers.Count > 0)
            message.CurrentDeliveryReceivers.Clear();

        IQueueDeliveryHandler deliveryHandler = _queue.Manager.DeliveryHandler;

        message.Decision = await deliveryHandler.BeginSend(_queue, message);
        if (!await _queue.ApplyDecision(message.Decision, message))
        {
            // The handler stopped the flow before anything was tracked: no ACK or timeout will release
            // the lock, and every later pull of the queue waited for it forever.
            _queue.ReleaseAcknowledgeLock(false);
            return false;
        }

        PutBackDecision beginPutBack = message.Decision.PutBack;

        //call before send and check decision
        bool canReceive = await deliveryHandler.CanConsumerReceive(_queue, message, requester.Client);
        if (!canReceive)
        {
            // The message has already been dequeued. Eligibility may also change between
            // messages of the same pull request, so retain it without changing its priority.
            if (!message.IsRemoved && message.Decision.PutBack == PutBackDecision.No)
                RestoreUndeliveredMessage(message);
            else
                _queue.ReleaseAcknowledgeLock(false);

            return false;
        }

        //create delivery object
        MessageDelivery delivery = new MessageDelivery(message, requester, deadline);

        bool tracked = false;
        while (!tracked)
        {
            tracked = await deliveryHandler.Tracker.Track(delivery);
            if (!tracked)
                await Task.Delay(10);
        }

        bool sent = await requester.Client.SendAsync(message.Message, headers);

        if (sent)
        {
            requester.ConsumeCount++;
            message.CurrentDeliveryReceivers.Add(requester);
            delivery.MarkAsSent();

            foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
                _ = handler.OnConsumed(_queue, delivery, requester.Client);

            //after all sending operations completed, calls implementation send completed method and complete the operation
            _queue.Info.AddMessageSend();

            if (!await _queue.ApplyDecision(message.Decision, message))
                return false;
        }
        else
        {
            deliveryHandler.Tracker.RemoveDelivery(delivery);
            Decision failed = await deliveryHandler.ConsumerReceiveFailed(_queue, delivery, requester.Client);
            message.Decision = failed;
            bool allowed = await _queue.ApplyDecision(failed, message);

            // Nothing reached the requester, and the delivery is no longer tracked, so no ACK or timeout
            // will release the lock: it used to stay held and every later pull of the queue waited forever.
            // A handler that stopped the flow, deleted the message or put it back owns it. Otherwise it
            // went back nowhere and was lost, so it is restored. EndSend is not called for a message
            // that was not sent (a persistent handler put it back a second time there).
            if (allowed && !message.IsRemoved && beginPutBack == PutBackDecision.No && failed.PutBack == PutBackDecision.No)
                RestoreUndeliveredMessage(message);
            else
                _queue.ReleaseAcknowledgeLock(false);

            // The requester is most likely gone: the rest of the batch is not sent
            return false;
        }

        message.Decision = await deliveryHandler.EndSend(_queue, message);
        await _queue.ApplyDecision(message.Decision, message);

        return true;
    }

    private void RestoreUndeliveredMessage(QueueMessage message)
    {
        try
        {
            if (!message.IsRemoved && !message.IsInQueue)
            {
                PushResult result = _queue.AddMessage(message, false);
                if (result != PushResult.Success)
                    throw new InvalidOperationException($"Cannot restore undelivered message in queue {_queue.Name}: {result}");
            }
        }
        finally
        {
            _queue.ReleaseAcknowledgeLock(false);
        }
    }

    public Task<PushResult> Push(QueueMessage message)
    {
        return Task.FromResult(PushResult.Success);
    }

    public Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus)
    {
        return Task.FromResult(QueueStatusAction.Allow);
    }

    public Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus)
    {
        return Task.FromResult(QueueStatusAction.Allow);
    }
}
