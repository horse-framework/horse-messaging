using System;
using Twino.MQ.Options;

namespace Twino.MQ.Data.Configuration
{
    internal static class ConfigurationMapper
    {
        internal static ChannelOptions ToOptions(this ChannelOptionsConfiguration configuration)
        {
            throw new NotImplementedException();
        }

        internal static ChannelQueueOptions ToOptions(this QueueOptionsConfiguration configuration)
        {
            throw new NotImplementedException();
        }

        internal static ChannelOptionsConfiguration ToConfiguration(this ChannelOptions options)
        {
            throw new NotImplementedException();
        }

        internal static QueueOptionsConfiguration ToConfiguration(this ChannelQueueOptions options)
        {
            throw new NotImplementedException();
        }
    }
}