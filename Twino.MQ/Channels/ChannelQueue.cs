using System;
using System.Collections;
using System.Collections.Generic;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Channels
{
    /// <summary>
    /// Queue status
    /// </summary>
    public enum QueueStatus
    {
        /// <summary>
        /// Queue messaging is running. Messages are accepted and sent to queueus.
        /// </summary>
        Running,

        /// <summary>
        /// Queue messages are accepted and queued but not pending
        /// </summary>
        Paused,

        /// <summary>
        /// Queue messages are not accepted.
        /// </summary>
        Stopped
    }

    /// <summary>
    /// Channel queue.
    /// Keeps queued messages and subscribed clients.
    /// </summary>
    public class ChannelQueue
    {
        #region Properties

        /// <summary>
        /// Channel of the queue
        /// </summary>
        public Channel Channel { get; }

        /// <summary>
        /// Queue content type
        /// </summary>
        public ushort ContentType { get; }

        /// <summary>
        /// Queue options.
        /// If null, channel default options will be used
        /// </summary>
        public ChannelQueueOptions Options { get; }

        /// <summary>
        /// Queue authenticator.
        /// If null, authentication will be disabled for the queue.
        /// This value does not affected by channel authenticator.
        /// </summary>
        public IQueueAuthenticator Authenticator { get; }

        /// <summary>
        /// Queue event handler.
        /// If not null, all methods will be called when events are occured.
        /// If null, server's default event handler will be used.
        /// </summary>
        public IQueueEventHandler EventHandler { get; }

        /// <summary>
        /// Queue messaging handler.
        /// If null, server's default delivery will be used.
        /// </summary>
        public IMessageDeliveryHandler DeliveryHandler { get; }


        private readonly FlexArray<QueueClient> _clients;

        /// <summary>
        /// Subscribed clients of the queue
        /// </summary>
        public IEnumerable<QueueClient> Clients => _clients.All();

        private readonly List<QueueMessage> _messages = new List<QueueMessage>();

        /// <summary>
        /// Queued messages
        /// </summary>
        public IEnumerable<QueueMessage> Messages => _messages;

        #endregion

        #region Constructors

        internal ChannelQueue(Channel channel,
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

        /// <summary>
        /// Adds a new message into the queue
        /// </summary>
        public void AddMessage(QueueMessage message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes the message from the queue
        /// </summary>
        public void RemoveMessage(QueueMessage message)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Subscription Actions

        /// <summary>
        /// Subscribes the client to the queue
        /// </summary>
        public ChannelQueue Subscribe(MqClient client)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes the client from the queue
        /// </summary>
        public ChannelQueue Unsubscribe(MqClient client)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}