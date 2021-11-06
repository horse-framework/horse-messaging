using System;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Data.Configuration
{
    internal static class ConfigurationMapper
    {
        internal static QueueOptions ToOptions(this QueueOptionsConfiguration configuration)
        {
            return new QueueOptions
                   {
                       Acknowledge = configuration.Acknowledge.ToAckDecision(),
                       Type = configuration.Type.ToQueueType(),
                       AcknowledgeTimeout = TimeSpan.FromMilliseconds(configuration.AcknowledgeTimeout),
                       MessageTimeout = TimeSpan.FromMilliseconds(configuration.MessageTimeout),
                       MessageLimit = configuration.MessageLimit,
                       MessageSizeLimit = configuration.MessageSizeLimit,
                       ClientLimit = configuration.ClientLimit,
                       DelayBetweenMessages = configuration.DelayBetweenMessages,
                       PutBackDelay = configuration.PutBackDelay,
                       AutoDestroy = configuration.AutoDestroy.ToQueueDestroy(),
                       PutBack = configuration.PutBack.ToPutBackDecision(),
                       CommitWhen = configuration.CommitWhen.ToCommitWhen()
                   };
        }

        internal static QueueOptionsConfiguration ToConfiguration(this QueueOptions options)
        {
            return new QueueOptionsConfiguration
                   {
                       Type = options.Type.ToQueueTypeString(),
                       Acknowledge = options.Acknowledge.ToQueueAckString(),
                       AcknowledgeTimeout = Convert.ToInt32(options.AcknowledgeTimeout.TotalMilliseconds),
                       MessageLimit = options.MessageLimit,
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

        private static string ToQueueTypeString(this QueueType type)
        {
            return type.ToString().ToLowerInvariant();
        }

        private static QueueType ToQueueType(this string text)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "push": return QueueType.Push;
                case "roundrobin": return QueueType.RoundRobin;
                case "pull": return QueueType.Pull;
                default: return QueueType.Push;
            }
        }

        private static string ToQueueAckString(this QueueAckDecision status)
        {
            switch (status)
            {
                case QueueAckDecision.JustRequest: return "just";
                case QueueAckDecision.WaitForAcknowledge: return "wait";
                default: return "none";
            }
        }

        private static QueueAckDecision ToAckDecision(this string text)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "just": return QueueAckDecision.JustRequest;
                case "wait": return QueueAckDecision.WaitForAcknowledge;
                default: return QueueAckDecision.None;
            }
        }
    }
}