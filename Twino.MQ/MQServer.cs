using System;
using System.Collections.Generic;
using System.Data;
using Twino.MQ.Channels;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Security;

namespace Twino.MQ
{
    public class MQServer
    {
        #region Properties

        public ServerOptions Options { get; }

        private readonly FlexArray<Channel> _channels;
        public IEnumerable<Channel> Channels => _channels.All();

        private readonly FlexArray<MqClient> _clients;
        public IEnumerable<MqClient> Clients => _clients.All();

        public IClientAuthenticator Authenticator { get; }
        public IMessageProcessFlow Flow { get; }

        public IChannelEventHandler DefaultChannelEventHandler { get; private set; }
        public IChannelAuthenticator DefaultChannelAuthenticator { get; private set; }
        public IQueueAuthenticator DefaultQueueAuthenticator { get; private set; }
        public IQueueEventHandler DefaultQueueEventHandler { get; private set; }
        public IMessageDeliveryHandler DefaultDeliveryHandler { get; private set; }

        #endregion

        #region Constructors - Init

        public MQServer(ServerOptions options, IMessageProcessFlow flow, IClientAuthenticator authenticator = null)
        {
            Options = options;
            Flow = flow;
            Authenticator = authenticator;

            _channels = new FlexArray<Channel>(options.ChannelCapacity);
            _clients = new FlexArray<MqClient>(options.ClientCapacity);
        }

        #endregion

        #region Set Default Interfaces

        public void SetDefaultChannelHandlers(IChannelEventHandler eventHandler,
                                              IChannelAuthenticator authenticator)
        {
            if (DefaultChannelEventHandler != null)
                throw new ReadOnlyException("Default channel event handler can be set only once");

            DefaultChannelEventHandler = eventHandler;

            if (DefaultChannelAuthenticator != null)
                throw new ReadOnlyException("Default channel authenticator can be set only once");

            DefaultChannelAuthenticator = authenticator;
        }

        public void SetDefaultQueueHandlers(IQueueEventHandler handler,
                                            IQueueAuthenticator authenticator,
                                            IMessageDeliveryHandler deliveryHandler)
        {
            if (DefaultQueueEventHandler != null)
                throw new ReadOnlyException("Default queue event handler can be set only once");

            DefaultQueueEventHandler = handler;

            if (DefaultQueueAuthenticator != null)
                throw new ReadOnlyException("Default queue authenticator can be set only once");

            DefaultQueueAuthenticator = authenticator;

            if (DefaultDeliveryHandler != null)
                throw new ReadOnlyException("Default message delivery handler can be set only once");

            DefaultDeliveryHandler = deliveryHandler;
        }

        #endregion

        #region Channel Actions

        /// <summary>
        /// Creates new channel with default options, without event handler and authenticator
        /// </summary>
        public Channel CreateChannel(string name)
        {
            return CreateChannel(name, DefaultChannelEventHandler, DefaultChannelAuthenticator);
        }

        /// <summary>
        /// Creates new channel with custom event handler, authenticator and default options
        /// </summary>
        public Channel CreateChannel(string name,
                                     IChannelEventHandler eventHandler,
                                     IChannelAuthenticator authenticator)
        {
            return CreateChannel(name, Options, eventHandler, authenticator);
        }

        /// <summary>
        /// Creates new channel with custom event handler, authenticator and options
        /// </summary>
        public Channel CreateChannel(string name,
                                     ChannelOptions options,
                                     IChannelEventHandler eventHandler,
                                     IChannelAuthenticator authenticator)
        {
            Channel channel = _channels.Find(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            if (channel != null)
                throw new DuplicateNameException("There is already a channel with same name: " + name);

            channel = new Channel(this, options, name, authenticator, eventHandler);
            _channels.Add(channel);
            return channel;
        }

        #endregion

        #region Client Actions

        public void AddClient()
        {
            throw new NotImplementedException();
        }

        public void RemoveClient()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}