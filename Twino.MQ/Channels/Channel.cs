using System;
using System.Collections.Generic;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Security;

namespace Twino.MQ.Channels
{
    /// <summary>
    /// Channel status
    /// </summary>
    public enum ChannelStatus
    {
        /// <summary>
        /// Channel queue messaging is running. Messages are accepted and sent to queueus.
        /// </summary>
        Running,

        /// <summary>
        /// Channel queue messages are accepted and queued but not pending
        /// </summary>
        Paused,

        /// <summary>
        /// Channel queue messages are not accepted.
        /// </summary>
        Stopped
    }

    /// <summary>
    /// Messaging Queue Channel
    /// </summary>
    public class Channel
    {
        #region Properties

        /// <summary>
        /// Unique channel name (not case-sensetive)
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Server of the channel
        /// </summary>
        public MQServer Server { get; }

        /// <summary>
        /// Channel options
        /// </summary>
        public ChannelOptions Options { get; }

        /// <summary>
        /// Channel status
        /// </summary>
        public ChannelStatus Status { get; private set; }

        /// <summary>
        /// Channel authenticator.
        /// If null, server's default channel authenticator will be used.
        /// </summary>
        public IChannelAuthenticator Authenticator { get; }

        private readonly FlexArray<QueueContentType> _allowedContentTypes = new FlexArray<QueueContentType>(264, 250);

        /// <summary>
        /// Allowed content type in this channel
        /// </summary>
        public IEnumerable<QueueContentType> AllowedContentTypes => _allowedContentTypes.All();

        /// <summary>
        /// Channel event handler
        /// </summary>
        public IChannelEventHandler EventHandler { get; }

        private readonly FlexArray<ChannelQueue> _queues;

        /// <summary>
        /// Active channel queues
        /// </summary>
        public IEnumerable<ChannelQueue> Queues => _queues.All();

        #endregion

        #region Constructors

        internal Channel(MQServer server,
                         ChannelOptions options,
                         string name,
                         IChannelAuthenticator authenticator,
                         IChannelEventHandler eventHandler)
        {
            Server = server;
            Options = options;
            Name = name;
            Status = ChannelStatus.Running;

            Authenticator = authenticator;
            EventHandler = eventHandler;

            _queues = new FlexArray<ChannelQueue>(options.QueueCapacity);
        }

        #endregion

        #region Status Actions

        /// <summary>
        /// Sets status of the channel
        /// </summary>
        public void SetStatus(ChannelStatus status)
        {
            Status = status;
        }

        #endregion

        #region Queue Actions

        /// <summary>
        /// Creates new queue in the channel
        /// </summary>
        public void CreateQueue(ushort contentType,
                                ChannelQueueOptions options,
                                IQueueAuthenticator authenticator,
                                IQueueEventHandler eventHandler,
                                IMessageDeliveryHandler deliveryHandler)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a queue from the channel
        /// </summary>
        public void RemoveQueue(ChannelQueue queue)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}