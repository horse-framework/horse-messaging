using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Twino.MQ.Channels;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Internal;
using Twino.MQ.Options;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Twino.MQ
{
    /// <summary>
    /// Twino Messaging Queue Server
    /// </summary>
    public class MQServer
    {
        #region Properties

        /// <summary>
        /// Messaging Queue Server Options
        /// </summary>
        public MqServerOptions Options { get; }

        private readonly FlexArray<Channel> _channels;

        /// <summary>
        /// All channels of the server
        /// </summary>
        public IEnumerable<Channel> Channels => _channels.All();

        private readonly FlexArray<MqClient> _clients;

        /// <summary>
        /// All connected clients in the server
        /// </summary>
        public IEnumerable<MqClient> Clients => _clients.All();

        /// <summary>
        /// Underlying Twino Server
        /// </summary>
        internal TwinoServer Server { get; private set; }

        /// <summary>
        /// Client authenticator implementation.
        /// If null, all clients will be accepted.
        /// </summary>
        public IClientAuthenticator Authenticator { get; }

        /// <summary>
        /// Complete message flow implementation, from receiving until delivery reported.
        /// </summary>
        public IMessageProcessFlow Flow { get; }

        /// <summary>
        /// Default channel event handler.
        /// If channels do not have their own custom event handlers, will event handler will run for them
        /// </summary>
        public IChannelEventHandler DefaultChannelEventHandler { get; private set; }

        /// <summary>
        /// Default channel authenticatır
        /// If channels do not have their own custom authenticator and this value is not null,
        /// this authenticator will authenticate the clients
        /// </summary>
        public IChannelAuthenticator DefaultChannelAuthenticator { get; private set; }

        /// <summary>
        /// Default queue event handler.
        /// If queues do not have their own custom event handlers, will event handler will run for them
        /// </summary>
        public IQueueEventHandler DefaultQueueEventHandler { get; private set; }

        /// <summary>
        /// Default message delivery handler for queues, if they do not have their custom delivery handler
        /// </summary>
        public IMessageDeliveryHandler DefaultDeliveryHandler { get; private set; }

        /// <summary>
        /// Id generator for messages from server 
        /// </summary>
        public IUniqueIdGenerator MessageIdGenerator { get; set; } = new DefaultUniqueIdGenerator();

        /// <summary>
        /// Id generator for clients which has no specified unique id 
        /// </summary>
        public IUniqueIdGenerator ClientIdGenerator { get; set; } = new DefaultUniqueIdGenerator();

        #endregion

        #region Constructors - Init

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        public MQServer(MqServerOptions options, IMessageProcessFlow flow, IClientAuthenticator authenticator = null)
        {
            Options = options;
            Flow = flow;
            Authenticator = authenticator;

            _channels = new FlexArray<Channel>(options.ChannelCapacity);
            _clients = new FlexArray<MqClient>(options.ClientCapacity);
        }

        #endregion

        #region Set Default Interfaces

        /// <summary>
        /// Sets default channel event handler and authenticator
        /// </summary>
        public void SetDefaultChannelHandlers(IChannelEventHandler eventHandler, IChannelAuthenticator authenticator)
        {
            if (DefaultChannelEventHandler != null)
                throw new ReadOnlyException("Default channel event handler can be set only once");

            DefaultChannelEventHandler = eventHandler;

            if (DefaultChannelAuthenticator != null)
                throw new ReadOnlyException("Default channel authenticator can be set only once");

            DefaultChannelAuthenticator = authenticator;
        }

        /// <summary>
        /// Sets default queue event handler, authenticator and message delivery handler
        /// </summary>
        public void SetDefaultQueueHandlers(IQueueEventHandler handler, IMessageDeliveryHandler deliveryHandler)
        {
            if (DefaultQueueEventHandler != null)
                throw new ReadOnlyException("Default queue event handler can be set only once");

            DefaultQueueEventHandler = handler;

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
            return CreateChannel(name, DefaultChannelAuthenticator, DefaultChannelEventHandler, DefaultDeliveryHandler);
        }

        /// <summary>
        /// Creates new channel with custom event handler, authenticator and default options
        /// </summary>
        public Channel CreateChannel(string name,
                                     IChannelAuthenticator authenticator,
                                     IChannelEventHandler eventHandler,
                                     IMessageDeliveryHandler deliveryHandler)
        {
            return CreateChannel(name, Options, authenticator, eventHandler, deliveryHandler);
        }

        /// <summary>
        /// Creates new channel with custom event handler, authenticator and options
        /// </summary>
        public Channel CreateChannel(string name,
                                     ChannelOptions options,
                                     IChannelAuthenticator authenticator,
                                     IChannelEventHandler eventHandler,
                                     IMessageDeliveryHandler deliveryHandler)
        {
            Channel channel = _channels.Find(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            if (channel != null)
                throw new DuplicateNameException("There is already a channel with same name: " + name);

            channel = new Channel(this, options, name, authenticator, eventHandler, deliveryHandler);
            _channels.Add(channel);
            return channel;
        }

        #endregion

        #region Client Actions

        /// <summary>
        /// Adds new client to the server
        /// </summary>
        internal void AddClient(MqClient client)
        {
            _clients.Add(client);
        }

        /// <summary>
        /// Removes the client from the server
        /// </summary>
        internal async Task RemoveClient(MqClient client)
        {
            _clients.Remove(client);

            foreach (Channel channel in _channels.All())
                await channel.RemoveClient(client);
        }

        /// <summary>
        /// Finds client from unique id
        /// </summary>
        public MqClient FindClient(string uniqueId)
        {
            return _clients.Find(x => x.UniqueId == uniqueId);
        }

        #endregion

        #region Start - Stop

        /// <summary>
        /// Starts server and accepts the TCP connections
        /// </summary>
        public void Start()
        {
            if (Flow == null)
                throw new NoNullAllowedException("Message process flow should be implemented");

            ServerOptions serverOptions = new ServerOptions();
            Server = new TwinoServer(serverOptions);
            MqConnectionHandler handler = new MqConnectionHandler(this);
            Server.UseTmq(handler);
            Server.Start();
        }

        /// <summary>
        /// Stops server
        /// </summary>
        public void Stop()
        {
            if (Server == null)
                throw new InvalidOperationException("Server stop error: Server is not running.");

            Server.Stop();
            Server = null;
        }

        #endregion
    }
}