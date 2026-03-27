using System;
using System.Collections.Generic;
using System.Reflection;
using EnumsNET;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

internal class QueueTypeResolver : ITypeDescriptorResolver<QueueTypeDescriptor>
{
    private readonly HorseClient _client;

    public QueueTypeResolver(HorseClient client)
    {
        _client = client;
    }

    public QueueTypeDescriptor Resolve(Type type, QueueTypeDescriptor defaultDescriptor)
    {
        QueueTypeDescriptor descriptor = new QueueTypeDescriptor();

        if (defaultDescriptor != null)
            ResolveDefaults(type, descriptor, defaultDescriptor);

        ResolveDescriptor(type, descriptor);

        if (_client.Queue.NameHandler != null && !IsConsumerType(type))
        {
            string queueName = _client.Queue.NameHandler.Invoke(new QueueNameHandlerContext
            {
                Type = type,
                Client = _client,
                QueueName = descriptor.QueueName
            });

            if (!string.IsNullOrEmpty(queueName))
            {
                descriptor.QueueName = queueName;
                descriptor.HasQueueName = true;
            }
        }


        return descriptor;
    }

    private bool IsConsumerType(Type type)
    {
        Type[] interfaceTypes = type.GetInterfaces();
        if (interfaceTypes.Length == 0)
            return false;

        Type openGenericType = typeof(IQueueConsumer<>);

        foreach (Type interfaceType in interfaceTypes)
        {
            if (!interfaceType.IsGenericType)
                continue;

            Type[] genericArgs = interfaceType.GetGenericArguments();
            if (genericArgs.Length != 1)
                continue;

            Type genericType = openGenericType.MakeGenericType(genericArgs[0]);
            if (type.IsAssignableTo(genericType))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves default values from model type configurator
    /// </summary>
    private void ResolveDefaults(Type type, QueueTypeDescriptor descriptor, QueueTypeDescriptor defaultConfigurator)
    {
        if (defaultConfigurator.HighPriority)
            descriptor.HighPriority = true;

        if (defaultConfigurator.PartitionLabel != null)
            descriptor.PartitionLabel = defaultConfigurator.PartitionLabel;

        if (defaultConfigurator.MaxPartitions.HasValue)
            descriptor.MaxPartitions = defaultConfigurator.MaxPartitions.Value;

        if (defaultConfigurator.SubscribersPerPartition.HasValue)
            descriptor.SubscribersPerPartition = defaultConfigurator.SubscribersPerPartition.Value;

        if (defaultConfigurator.QueueType.HasValue)
            descriptor.QueueType = defaultConfigurator.QueueType.Value;

        if (defaultConfigurator.ClientLimit.HasValue)
            descriptor.ClientLimit = defaultConfigurator.ClientLimit.Value;

        if (!string.IsNullOrEmpty(defaultConfigurator.Topic))
            descriptor.Topic = defaultConfigurator.Topic;

        if (!string.IsNullOrEmpty(defaultConfigurator.QueueName))
        {
            descriptor.QueueName = defaultConfigurator.QueueName;
            descriptor.HasQueueName = true;
        }

        if (defaultConfigurator.PutBackDecision.HasValue)
            descriptor.PutBackDecision = defaultConfigurator.PutBackDecision.Value;

        if (defaultConfigurator.PutBackDelay.HasValue)
            descriptor.PutBackDelay = defaultConfigurator.PutBackDelay.Value;

        if (defaultConfigurator.DelayBetweenMessages.HasValue)
            descriptor.DelayBetweenMessages = defaultConfigurator.DelayBetweenMessages.Value;

        if (defaultConfigurator.Acknowledge.HasValue)
            descriptor.Acknowledge = defaultConfigurator.Acknowledge.Value;

        if (defaultConfigurator.MessageTimeout != null)
            descriptor.MessageTimeout = defaultConfigurator.MessageTimeout;

        if (defaultConfigurator.AcknowledgeTimeout.HasValue)
            descriptor.AcknowledgeTimeout = defaultConfigurator.AcknowledgeTimeout.Value;

        if (defaultConfigurator.UniqueIdCheck.HasValue)
            descriptor.UniqueIdCheck = defaultConfigurator.UniqueIdCheck.Value;

        if (defaultConfigurator.AutoAck)
            descriptor.AutoAck = true;

        if (defaultConfigurator.AutoNack)
        {
            descriptor.AutoNack = true;
            descriptor.AutoNackReason = defaultConfigurator.AutoNackReason;
        }

        if (defaultConfigurator.Retry != null)
            descriptor.Retry = CloneRetry(defaultConfigurator.Retry);

        if (!string.IsNullOrEmpty(defaultConfigurator.MoveOnErrorQueueName))
        {
            descriptor.MoveOnErrorQueueName = defaultConfigurator.MoveOnErrorQueueName;
            descriptor.MoveOnErrorQueueTopic = defaultConfigurator.MoveOnErrorQueueTopic;
        }

        if (defaultConfigurator.DefaultPushException != null)
            descriptor.DefaultPushException = defaultConfigurator.DefaultPushException;

        if (defaultConfigurator.PushExceptions.Count > 0)
            descriptor.PushExceptions.AddRange(defaultConfigurator.PushExceptions);

        if (defaultConfigurator.DefaultPublishException != null)
            descriptor.DefaultPublishException = defaultConfigurator.DefaultPublishException;

        if (defaultConfigurator.PublishExceptions.Count > 0)
            descriptor.PublishExceptions.AddRange(defaultConfigurator.PublishExceptions);

        if (defaultConfigurator.Headers.Count > 0)
            descriptor.Headers.AddRange(defaultConfigurator.Headers);
    }

    /// <summary>
    /// Resolves base and common descriptor attributes of the type and fillds the descriptor object values
    /// </summary>
    private void ResolveDescriptor(Type type, QueueTypeDescriptor descriptor)
    {
        descriptor.Type = type;

        HighPriorityMessageAttribute prioAttr = type.GetCustomAttribute<HighPriorityMessageAttribute>(true);
        if (prioAttr != null)
            descriptor.HighPriority = true;

        AcknowledgeAttribute ackAttr = type.GetCustomAttribute<AcknowledgeAttribute>(true);
        if (ackAttr != null)
            descriptor.Acknowledge = ackAttr.Value;

        QueueTypeAttribute typeAttr = type.GetCustomAttribute<QueueTypeAttribute>(true);
        if (typeAttr != null)
            descriptor.QueueType = typeAttr.Type;

        QueueTopicAttribute topicAttr = type.GetCustomAttribute<QueueTopicAttribute>(true);
        if (topicAttr != null)
            descriptor.Topic = topicAttr.Topic;

        ClientLimitAttribute clientLimitAttr = type.GetCustomAttribute<ClientLimitAttribute>(true);
        if (clientLimitAttr != null)
            descriptor.ClientLimit = clientLimitAttr.Value;

        UniqueIdCheckAttribute uidAttr = type.GetCustomAttribute<UniqueIdCheckAttribute>(true);
        if (uidAttr != null)
            descriptor.UniqueIdCheck = uidAttr.Value;

        MessageTimeoutAttribute msgTimeoutAttr = type.GetCustomAttribute<MessageTimeoutAttribute>(true);
        if (msgTimeoutAttr != null)
            descriptor.MessageTimeout = new MessageTimeoutStrategyInfo(msgTimeoutAttr.Duration, msgTimeoutAttr.Policy.AsString(EnumFormat.Description), msgTimeoutAttr.TargetName ?? string.Empty);

        AcknowledgeTimeoutAttribute ackTimeoutAttr = type.GetCustomAttribute<AcknowledgeTimeoutAttribute>(true);
        if (ackTimeoutAttr != null)
            descriptor.AcknowledgeTimeout = ackTimeoutAttr.Value;

        QueueNameAttribute queueNameAttribute = type.GetCustomAttribute<QueueNameAttribute>(true);
        if (queueNameAttribute != null)
        {
            descriptor.HasQueueName = true;
            descriptor.QueueName = string.IsNullOrEmpty(queueNameAttribute.Name) ? type.Name : queueNameAttribute.Name;
        }
        else if (!descriptor.HasQueueName)
            descriptor.QueueName = type.Name;

        DelayBetweenMessagesAttribute delayAttr = type.GetCustomAttribute<DelayBetweenMessagesAttribute>(true);
        if (delayAttr != null)
            descriptor.DelayBetweenMessages = delayAttr.Value;

        PutBackAttribute putbackAttr = type.GetCustomAttribute<PutBackAttribute>(true);
        if (putbackAttr != null)
        {
            descriptor.PutBackDecision = putbackAttr.Value;
            descriptor.PutBackDelay = putbackAttr.Delay;
        }

        MoveOnErrorAttribute moveOnErrorAttr = type.GetCustomAttribute<MoveOnErrorAttribute>(true);
        if (moveOnErrorAttr != null)
        {
            descriptor.MoveOnErrorQueueName = moveOnErrorAttr.QueueName;
            descriptor.MoveOnErrorQueueTopic = moveOnErrorAttr.QueueTopic;
        }

        IEnumerable<MessageHeaderAttribute> headerAttributes = type.GetCustomAttributes<MessageHeaderAttribute>(true);
        foreach (MessageHeaderAttribute headerAttribute in headerAttributes)
            descriptor.Headers.Add(new KeyValuePair<string, string>(headerAttribute.Key, headerAttribute.Value));

        // ── Partition attribute ───────────────────────────────────────────────
        PartitionedQueueAttribute partAttr = type.GetCustomAttribute<PartitionedQueueAttribute>(true);
        if (partAttr != null)
        {
            descriptor.PartitionLabel = partAttr.Label ?? string.Empty;
            descriptor.MaxPartitions = partAttr.MaxPartitions >= 0 ? partAttr.MaxPartitions : null;
            descriptor.SubscribersPerPartition = partAttr.SubscribersPerPartition >= 0 ? partAttr.SubscribersPerPartition : null;
        }
    }

    private static RetryAttribute CloneRetry(RetryAttribute retry)
    {
        if (retry == null)
            return null;

        return new RetryAttribute(retry.Count, retry.DelayBetweenRetries)
        {
            IgnoreExceptions = retry.IgnoreExceptions == null ? null : (Type[]) retry.IgnoreExceptions.Clone()
        };
    }
}
