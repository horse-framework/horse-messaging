using System;
using System.Collections;
using System.Collections.Generic;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
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
        #region Properties

        public Channel Channel { get; }
        public ushort ContentType { get; }
        public ChannelQueueOptions Options { get; }

        public IQueueAuthenticator Authenticator { get; }
        public IQueueEventHandler EventHandler { get; }
        public IMessageDeliveryHandler DeliveryHandler { get; }


        private readonly FlexArray<QueueClient> _clients;
        public IEnumerable<QueueClient> Clients => _clients.All();

        private readonly List<QueueMessage> _messages = new List<QueueMessage>();
        public IEnumerable<QueueMessage> Messages => _messages;

        #endregion

        #region Constructors

        public ChannelQueue(Channel channel,
                            ushort contentType,
                            ChannelQueueOptions options,
                            IQueueAuthenticator authenticator,
                            IQueueEventHandler eventHandler,
                            IMessageDeliveryHandler deliveryHandler)
        {
            Channel = channel;
            ContentType = contentType;
            Options = options;

            Authenticator = authenticator;
            EventHandler = eventHandler;
            DeliveryHandler = deliveryHandler;

            _clients = new FlexArray<QueueClient>(channel.Server.Options.ClientCapacity);
        }

        #endregion

        #region Messaging Actions

        public void AddMessage()
        {
            throw new NotImplementedException();
        }

        public void RemoveMessage()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Subscription Actions

        public void Subscribe()
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}