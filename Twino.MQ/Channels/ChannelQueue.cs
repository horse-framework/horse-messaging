using System;
using System.Collections;
using System.Collections.Generic;
using Twino.MQ.Clients;
using Twino.MQ.Options;
using Twino.MQ.Security;

namespace Twino.MQ.Channels
{
    public enum QueueStatus
    {
        None,
        Running,
        Paused,
        Stopped
    }

    public class ChannelQueue
    {
        public ushort ContentType { get; set; }

        public Channel Channel { get; set; }

        public ChannelQueueOptions Options { get; set; }

        public IQueueAuthenticator Authenticator { get; set; }

        public IEnumerable<QueueClient> Clients { get; set; }

        public Queue<QueueMessage> Messages { get; set; }

        public IQueueEventHandler EventHandler { get; set; }
        
        public IMessageDeliveryHandler DeliveryHandler { get; set; }

        public void AddMessage()
        {
            throw new NotImplementedException();
        }

        public void RemoveMessage()
        {
            throw new NotImplementedException();
        }

        public void Subscribe()
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe()
        {
            throw new NotImplementedException();
        }
    }
}