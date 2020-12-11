using System;
using Horse.Mq.Options;
using Horse.Mq.Queues;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Data.Configuration
{
    internal static class ConfigurationMapper
    {
        internal static QueueOptions ToOptions(this QueueOptionsConfiguration configuration)
        {
            return new QueueOptions
                   {
                       Acknowledge = configuration.Acknowledge.ToAckDecision(),
                       Status = configuration.Status.ToQueueStatus(),
                       AcknowledgeTimeout = TimeSpan.FromMilliseconds(configuration.AcknowledgeTimeout),
                       MessageTimeout = TimeSpan.FromMilliseconds(configuration.AcknowledgeTimeout),
                       MessageLimit = configuration.MessageLimit,
                       HideClientNames = configuration.HideClientNames,
                       MessageSizeLimit = configuration.MessageSizeLimit,
                       UseMessageId = configuration.UseMessageId,
                       ClientLimit = configuration.ClientLimit,
                       DelayBetweenMessages = configuration.DelayBetweenMessages,
                       PutBackDelay = configuration.PutBackDelay
                   };
        }

        internal static QueueOptionsConfiguration ToConfiguration(this QueueOptions options)
        {
            return new QueueOptionsConfiguration
                   {
                       Status = options.Status.ToStatusString(),
                       Acknowledge = options.Acknowledge.ToQueueAckString(),
                       AcknowledgeTimeout = Convert.ToInt32(options.AcknowledgeTimeout.TotalMilliseconds),
                       MessageLimit = options.MessageLimit,
                       MessageTimeout = Convert.ToInt32(options.MessageTimeout.TotalMilliseconds),
                       HideClientNames = options.HideClientNames,
                       MessageSizeLimit = options.MessageSizeLimit,
                       UseMessageId = options.UseMessageId,
                       ClientLimit = options.ClientLimit,
                       DelayBetweenMessages = options.DelayBetweenMessages,
                       PutBackDelay = options.PutBackDelay
                   };
        }

        private static string ToStatusString(this QueueStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }

        private static QueueStatus ToQueueStatus(this string text)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "broadcast": return QueueStatus.Broadcast;
                case "push": return QueueStatus.Push;
                case "roundrobin": return QueueStatus.RoundRobin;
                case "pull": return QueueStatus.Pull;
                case "cache": return QueueStatus.Cache;
                case "paused": return QueueStatus.Paused;
                default: return QueueStatus.Stopped;
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