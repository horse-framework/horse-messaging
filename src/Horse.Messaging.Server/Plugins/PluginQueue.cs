using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnumsNET;
using Horse.Messaging.Plugins;
using Horse.Messaging.Plugins.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Plugins;

internal class PluginQueue : IPluginQueue
{
    private readonly HorseQueue _queue;

    public string Name => _queue.Name;
    public string Topic => _queue.Topic;
    public PluginQueueStatus Status => (PluginQueueStatus)_queue.Status;
    public PluginQueueType Type => (PluginQueueType)_queue.Type;
    public string ManagerName => _queue.ManagerName;

    public PluginQueue(HorseQueue queue)
    {
        _queue = queue;
    }

    public QueueInformation GetInfo()
    {
        string ack = "none";
        if (_queue.Options.Acknowledge == QueueAckDecision.JustRequest)
            ack = "just";
        else if (_queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge)
            ack = "wait";

        return new QueueInformation
        {
            Name = _queue.Name,
            Topic = _queue.Topic,
            Status = _queue.Status.ToString().Trim().ToLower(),
            PriorityMessages = _queue.Manager == null ? 0 : _queue.Manager.PriorityMessageStore.Count(),
            Messages = _queue.Manager == null ? 0 : _queue.Manager.MessageStore.Count(),
            ProcessingMessages = _queue.Clients.Count(x => x.CurrentlyProcessing != null),
            DeliveryTrackingMessages = _queue.Manager == null ? 0 : _queue.Manager.DeliveryHandler.Tracker.GetDeliveryCount(),
            Acknowledge = ack,
            AcknowledgeTimeout = Convert.ToInt32(_queue.Options.AcknowledgeTimeout.TotalMilliseconds),
            MessageTimeout = _queue.Options.MessageTimeout,
            ReceivedMessages = _queue.Info.ReceivedMessages,
            SentMessages = _queue.Info.SentMessages,
            NegativeAcks = _queue.Info.NegativeAcknowledge,
            Acks = _queue.Info.Acknowledges,
            TimeoutMessages = _queue.Info.TimedOutMessages,
            SavedMessages = _queue.Info.MessageSaved,
            RemovedMessages = _queue.Info.MessageRemoved,
            Errors = _queue.Info.ErrorCount,
            LastMessageReceived = _queue.Info.GetLastMessageReceiveUnix(),
            LastMessageSent = _queue.Info.GetLastMessageSendUnix(),
            MessageLimit = _queue.Options.MessageLimit,
            LimitExceededStrategy = _queue.Options.LimitExceededStrategy.AsString(EnumFormat.Description),
            MessageSizeLimit = _queue.Options.MessageSizeLimit,
            DelayBetweenMessages = _queue.Options.DelayBetweenMessages
        };
    }

    public void SetStatus(PluginQueueStatus newStatus)
    {
        _queue.SetStatus((QueueStatus)newStatus);
    }

    public async Task<PluginPushResult> Push(string message, bool highPriority = false)
    {
        PushResult result = await _queue.Push(message, highPriority);
        return (PluginPushResult)result;
    }

    public async Task<PluginPushResult> Push(HorseMessage message)
    {
        PushResult result = await _queue.Push(message);
        return (PluginPushResult)result;
    }

    public IEnumerable<IPluginMessagingClient> GetConsumers()
    {
        return _queue.Clients.Select(x => new PluginMessagingClient(x.Client));
    }

    public PluginQueueOptions GetOptions()
    {
        return CreateFrom(_queue.Options);
    }

    public void SetOptions(Action<PluginQueueOptions> options)
    {
        var o = CreateFrom(_queue.Options);
        options(o);
        ApplyTo(o, _queue.Options);
    }

    internal static PluginQueueOptions CreateFrom(QueueOptions options)
    {
        return new PluginQueueOptions
        {
            Acknowledge = options.Acknowledge,
            Type = (PluginQueueType)options.Type,
            AcknowledgeTimeout = options.AcknowledgeTimeout,
            AutoDestroy = (PluginQueueDestroy)options.AutoDestroy,
            AutoQueueCreation = options.AutoQueueCreation,
            ClientLimit = options.ClientLimit,
            CommitWhen = (PluginCommitWhen)options.CommitWhen,
            DelayBetweenMessages = options.DelayBetweenMessages,
            LimitExceededStrategy = (PluginMessageLimitExceededStrategy)options.LimitExceededStrategy,
            MessageIdUniqueCheck = options.MessageIdUniqueCheck,
            MessageLimit = options.MessageLimit,
            MessageSizeLimit = options.MessageSizeLimit,
            MessageTimeout = options.MessageTimeout,
            PutBack = (PluginPutBackDecision)options.PutBack,
            PutBackDelay = options.PutBackDelay
        };
    }

    internal static void ApplyTo(PluginQueueOptions options, QueueOptions target)
    {
        target.Acknowledge = options.Acknowledge;
        target.Type = (QueueType)options.Type;
        target.AcknowledgeTimeout = options.AcknowledgeTimeout;
        target.AutoDestroy = (QueueDestroy)options.AutoDestroy;
        target.AutoQueueCreation = options.AutoQueueCreation;
        target.ClientLimit = options.ClientLimit;
        target.CommitWhen = (CommitWhen)options.CommitWhen;
        target.DelayBetweenMessages = options.DelayBetweenMessages;
        target.LimitExceededStrategy = (MessageLimitExceededStrategy)options.LimitExceededStrategy;
        target.MessageIdUniqueCheck = options.MessageIdUniqueCheck;
        target.MessageLimit = options.MessageLimit;
        target.MessageSizeLimit = options.MessageSizeLimit;
        target.MessageTimeout = options.MessageTimeout;
        target.PutBack = (PutBackDecision)options.PutBack;
        target.PutBackDelay = options.PutBackDelay;
    }
}