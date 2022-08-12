using System;
using EnumsNET;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Data.Configuration
{
    internal static class ConfigurationMapper
    {
        internal static QueueOptions ToOptions(this QueueOptionsConfiguration configuration)
        {
            return new QueueOptions
            {
                Acknowledge = Enums.Parse<QueueAckDecision>(configuration.Acknowledge, true, EnumFormat.Description),
                Type = Enums.Parse<QueueType>(configuration.Type, true, EnumFormat.Description),
                AcknowledgeTimeout = TimeSpan.FromMilliseconds(configuration.AcknowledgeTimeout),
                MessageTimeout = TimeSpan.FromMilliseconds(configuration.MessageTimeout),
                MessageLimit = configuration.MessageLimit,
                LimitExceededStrategy = Enums.Parse<MessageLimitExceededStrategy>(configuration.LimitExceededStrategy, true, EnumFormat.Description),
                MessageSizeLimit = configuration.MessageSizeLimit,
                ClientLimit = configuration.ClientLimit,
                DelayBetweenMessages = configuration.DelayBetweenMessages,
                PutBackDelay = configuration.PutBackDelay,
                AutoDestroy = Enums.Parse<QueueDestroy>(configuration.AutoDestroy, true, EnumFormat.Description),
                PutBack = Enums.Parse<PutBackDecision>(configuration.PutBack, true, EnumFormat.Description),
                CommitWhen = Enums.Parse<CommitWhen>(configuration.CommitWhen, true, EnumFormat.Description)
            };
        }

        internal static QueueOptionsConfiguration ToConfiguration(this QueueOptions options)
        {
            return new QueueOptionsConfiguration
            {
                Type = options.Type.AsString(EnumFormat.Description),
                Acknowledge = options.Acknowledge.AsString(EnumFormat.Description),
                AcknowledgeTimeout = Convert.ToInt32(options.AcknowledgeTimeout.TotalMilliseconds),
                MessageLimit = options.MessageLimit,
                LimitExceededStrategy = options.LimitExceededStrategy.ToString(),
                MessageTimeout = Convert.ToInt32(options.MessageTimeout.TotalMilliseconds),
                MessageSizeLimit = options.MessageSizeLimit,
                ClientLimit = options.ClientLimit,
                DelayBetweenMessages = options.DelayBetweenMessages,
                PutBack = options.PutBack.ToString(),
                PutBackDelay = options.PutBackDelay,
                AutoDestroy = options.AutoDestroy.ToString(),
                CommitWhen = options.CommitWhen.ToString()
            };
        }
    }
}