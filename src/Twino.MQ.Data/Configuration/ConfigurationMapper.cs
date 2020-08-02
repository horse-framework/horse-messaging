using System;
using Twino.MQ.Options;
using Twino.MQ.Queues;

namespace Twino.MQ.Data.Configuration
{
    internal static class ConfigurationMapper
    {
        internal static ChannelOptions ToOptions(this ChannelOptionsConfiguration configuration)
        {
            return new ChannelOptions
                   {
                       Status = configuration.Status.ToQueueStatus(),
                       AcknowledgeTimeout = TimeSpan.FromMilliseconds(configuration.AcknowledgeTimeout),
                       MessageTimeout = TimeSpan.FromMilliseconds(configuration.AcknowledgeTimeout),
                       AllowedQueues = configuration.AllowedQueues,
                       ClientLimit = configuration.ClientLimit,
                       MessageLimit = configuration.MessageLimit,
                       QueueLimit = configuration.QueueLimit,
                       RequestAcknowledge = configuration.RequestAcknowledge,
                       TagName = configuration.TagName,
                       AllowMultipleQueues = configuration.AllowMultipleQueues,
                       DestroyWhenEmpty = configuration.DestroyWhenEmpty,
                       HideClientNames = configuration.HideClientNames,
                       MessageSizeLimit = configuration.MessageSizeLimit,
                       UseMessageId = configuration.UseMessageId,
                       WaitForAcknowledge = configuration.WaitForAcknowledge,
                       SendOnlyFirstAcquirer = configuration.SendOnlyFirstAcquirer
                   };
        }

        internal static ChannelQueueOptions ToOptions(this QueueOptionsConfiguration configuration)
        {
            return new ChannelQueueOptions
                   {
                       Status = configuration.Status.ToQueueStatus(),
                       AcknowledgeTimeout = TimeSpan.FromMilliseconds(configuration.AcknowledgeTimeout),
                       MessageTimeout = TimeSpan.FromMilliseconds(configuration.AcknowledgeTimeout),
                       MessageLimit = configuration.MessageLimit,
                       RequestAcknowledge = configuration.RequestAcknowledge,
                       TagName = configuration.TagName,
                       HideClientNames = configuration.HideClientNames,
                       MessageSizeLimit = configuration.MessageSizeLimit,
                       UseMessageId = configuration.UseMessageId,
                       WaitForAcknowledge = configuration.WaitForAcknowledge,
                       SendOnlyFirstAcquirer = configuration.SendOnlyFirstAcquirer
                   };
        }

        internal static ChannelOptionsConfiguration ToConfiguration(this ChannelOptions options)
        {
            return new ChannelOptionsConfiguration
                   {
                       Status = options.Status.ToStatusString(),
                       AcknowledgeTimeout = Convert.ToInt32(options.AcknowledgeTimeout.TotalMilliseconds),
                       AllowedQueues = options.AllowedQueues,
                       ClientLimit = options.ClientLimit,
                       MessageLimit = options.MessageLimit,
                       MessageTimeout = Convert.ToInt32(options.MessageTimeout.TotalMilliseconds),
                       QueueLimit = options.QueueLimit,
                       RequestAcknowledge = options.RequestAcknowledge,
                       TagName = options.TagName,
                       AllowMultipleQueues = options.AllowMultipleQueues,
                       DestroyWhenEmpty = options.DestroyWhenEmpty,
                       HideClientNames = options.HideClientNames,
                       MessageSizeLimit = options.MessageSizeLimit,
                       UseMessageId = options.UseMessageId,
                       WaitForAcknowledge = options.WaitForAcknowledge,
                       SendOnlyFirstAcquirer = options.SendOnlyFirstAcquirer
                   };
        }

        internal static QueueOptionsConfiguration ToConfiguration(this ChannelQueueOptions options)
        {
            return new QueueOptionsConfiguration
                   {
                       Status = options.Status.ToStatusString(),
                       AcknowledgeTimeout = Convert.ToInt32(options.AcknowledgeTimeout.TotalMilliseconds),
                       MessageLimit = options.MessageLimit,
                       MessageTimeout = Convert.ToInt32(options.MessageTimeout.TotalMilliseconds),
                       RequestAcknowledge = options.RequestAcknowledge,
                       TagName = options.TagName,
                       HideClientNames = options.HideClientNames,
                       MessageSizeLimit = options.MessageSizeLimit,
                       UseMessageId = options.UseMessageId,
                       WaitForAcknowledge = options.WaitForAcknowledge,
                       SendOnlyFirstAcquirer = options.SendOnlyFirstAcquirer
                   };
        }

        private static string ToStatusString(this QueueStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }

        private static QueueStatus ToQueueStatus(this string text)
        {
            switch (text.ToLowerInvariant())
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
    }
}