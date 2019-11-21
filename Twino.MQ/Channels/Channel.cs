using System;
using System.Collections;
using System.Collections.Generic;
using Twino.MQ.Clients;
using Twino.MQ.Options;
using Twino.MQ.Security;

namespace Twino.MQ.Channels
{
    public enum ChannelStatus
    {
        None,
        Running,
        Paused,
        Stopped
    }

    public class Channel
    {
        public ChannelStatus Status { get; set; }

        public string Name { get; set; }

        public IChannelAuthenticator Authenticator { get; set; }

        public IEnumerable<ushort> AllowedContentTypes { get; set; }

        public ChannelOptions Options { get; set; }
        
        public IChannelEventHandler EventHandler { get; set; }
        
        public MQServer Server { get; set; }

        public IEnumerable<ChannelQueue> Queues { get; set; }
        
        public IEnumerable<ChannelClient> Clients { get; set; }

        public void CreateQueue()
        {
            throw new NotImplementedException();
        }

        public void RemoveQueue()
        {
            throw new NotImplementedException();
        }

        public void Join(MqClient client)
        {
            throw new NotImplementedException();
        }

        public void Leave(MqClient client)
        {
            throw new NotImplementedException();
        }
    }
}