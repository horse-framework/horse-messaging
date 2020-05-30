using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Events;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.MQ.Routing;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Twino.MQ
{
    /// <summary>
    /// Twino Messaging Queue Server
    /// </summary>
    public class MqServer
    {
        #region Properties

        /// <summary>
        /// Messaging Queue Server Options
        /// </summary>
        public MqServerOptions Options { get; }

        private readonly SafeList<Channel> _channels;

        /// <summary>
        /// All channels of the server
        /// </summary>
        public IEnumerable<Channel> Channels => _channels.GetAsClone();

        private readonly SafeList<MqClient> _clients;

        /// <summary>
        /// All connected clients in the server
        /// </summary>
        public IEnumerable<MqClient> Clients => _clients.GetAsClone();

        private readonly SafeList<IRouter> _routers;

        /// <summary>
        /// All channels of the server
        /// </summary>
        public IEnumerable<IRouter> Routers => _routers.GetAsClone();

        /// <summary>
        /// Underlying Twino Server
        /// </summary>
        public TwinoServer Server { get; internal set; }

        /// <summary>
        /// Node server for distribitued systems
        /// </summary>
        public InstanceManager InstanceManager { get; }

        /// <summary>
        /// Client authenticator implementation.
        /// If null, all clients will be accepted.
        /// </summary>
        public IClientAuthenticator Authenticator { get; }

        /// <summary>
        /// Authorization implementation for client operations
        /// </summary>
        public IClientAuthorization Authorization { get; set; }

        /// <summary>
        /// Authorization implementation for administration operations
        /// </summary>
        public IAdminAuthorization AdminAuthorization { get; set; }

        /// <summary>
        /// Client connect and disconnect operations
        /// </summary>
        public IClientHandler ClientHandler { get; set; }

        /// <summary>
        /// Client message received handler (for only server-type messages)
        /// </summary>
        public IServerMessageHandler ServerMessageHandler { get; set; }

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

        /// <summary>
        /// Implementation registry library.
        /// Implementation instances are kept in this registry by their keys.
        /// </summary>
        public ImplementationRegistry Registry { get; } = new ImplementationRegistry();

        /// <summary>
        /// Locker object for preventing to create duplicated channels when requests are concurrent and auto channel creation is enabled
        /// </summary>
        private readonly SemaphoreSlim _findOrCreateChannelLocker = new SemaphoreSlim(1, 1);

        #endregion

        #region Events

        /// <summary>
        /// Triggered when a client is connected 
        /// </summary>
        public ClientEventManager OnClientConnected { get; set; }

        /// <summary>
        /// Triggered when a client is disconnected 
        /// </summary>
        public ClientEventManager OnClientDisconnected { get; set; }

        /// <summary>
        /// Triggered when a channel is created 
        /// </summary>
        public ChannelEventManager OnChannelCreated { get; set; }

        /// <summary>
        /// Triggered when a channel is removed 
        /// </summary>
        public ChannelEventManager OnChannelRemoved { get; set; }

        /// <summary>
        /// Triggered when a node is connected
        /// </summary>
        public NodeEventManager OnNodeConnected { get; set; }

        /// <summary>
        /// Triggered when a node is disconnected 
        /// </summary>
        public NodeEventManager OnNodeDisconnected { get; set; }

        #endregion

        #region Constructors - Init

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        public MqServer(IClientAuthenticator authenticator = null, IClientAuthorization authorization = null)
            : this(null, authenticator, authorization)
        {
        }

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        public MqServer(MqServerOptions options,
                        IClientAuthenticator authenticator = null,
                        IClientAuthorization authorization = null)
        {
            Options = options ?? new MqServerOptions();
            Authenticator = authenticator;
            Authorization = authorization;

            _routers = new SafeList<IRouter>(256);
            _channels = new SafeList<Channel>(256);
            _clients = new SafeList<MqClient>(2048);

            InstanceManager = new InstanceManager(this);

            InstanceManager.Initialize();

            OnClientConnected = new ClientEventManager(EventNames.ClientConnected, this);
            OnClientDisconnected = new ClientEventManager(EventNames.ClientDisconnected, this);
            OnChannelCreated = new ChannelEventManager(EventNames.ChannelCreated, this);
            OnChannelRemoved = new ChannelEventManager(EventNames.ChannelRemoved, this);
            OnNodeConnected = new NodeEventManager(EventNames.NodeConnected, this);
            OnNodeDisconnected = new NodeEventManager(EventNames.NodeDisconnected, this);
        }

        #endregion

        #region Set Default Interfaces

        /// <summary>
        /// Sets default channel event handler and authenticator
        /// </summary>
        /// <exception cref="ReadOnlyException">Thrown when default channel event handler or default channel authenticator already is set</exception>
        public void SetDefaultChannelHandler(IChannelEventHandler eventHandler, IChannelAuthenticator authenticator)
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
        /// <exception cref="ReadOnlyException">Thrown when default message delivery handler already is set</exception>
        public void SetDefaultDeliveryHandler(IMessageDeliveryHandler deliveryHandler)
        {
            if (DefaultDeliveryHandler != null)
                throw new ReadOnlyException("Default message delivery handler can be set only once");

            DefaultDeliveryHandler = deliveryHandler;
        }

        #endregion

        #region Channel Actions

        /// <summary>
        /// Creates new channel with default options, without event handler and authenticator
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when channel limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a channel with same name</exception>
        public Channel CreateChannel(string name)
        {
            if (DefaultDeliveryHandler == null)
                throw new NoNullAllowedException("There is no default delivery handler defined. Channel must have it's own delivery handler.");

            return CreateChannel(name, DefaultChannelAuthenticator, DefaultChannelEventHandler, DefaultDeliveryHandler);
        }

        /// <summary>
        /// Creates new channel with default options, without event handler and authenticator
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when channel limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a channel with same name</exception>
        public Channel CreateChannel(string name, Action<ChannelOptions> optionsAction)
        {
            if (DefaultDeliveryHandler == null)
                throw new NoNullAllowedException("There is no default delivery handler defined. Channel must have it's own delivery handler.");

            return CreateChannel(name, DefaultChannelAuthenticator, DefaultChannelEventHandler, DefaultDeliveryHandler, optionsAction);
        }

        /// <summary>
        /// Creates new channel with custom event handler, authenticator and default options
        /// </summary>
        /// <exception cref="OperationCanceledException">Thrown when channel limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a channel with same name</exception>
        public Channel CreateChannel(string name,
                                     IChannelAuthenticator authenticator,
                                     IChannelEventHandler eventHandler,
                                     IMessageDeliveryHandler deliveryHandler)
        {
            ChannelOptions options = ChannelOptions.CloneFrom(Options);
            return CreateChannel(name, authenticator, eventHandler, deliveryHandler, options);
        }

        /// <summary>
        /// Creates new channel with custom event handler, authenticator and options
        /// </summary>
        /// <exception cref="OperationCanceledException">Thrown when channel limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a channel with same name</exception>
        public Channel CreateChannel(string name,
                                     IChannelAuthenticator authenticator,
                                     IChannelEventHandler eventHandler,
                                     IMessageDeliveryHandler deliveryHandler,
                                     Action<ChannelOptions> optionsAction)
        {
            ChannelOptions options = ChannelOptions.CloneFrom(Options);
            optionsAction(options);
            return CreateChannel(name, authenticator, eventHandler, deliveryHandler, options);
        }

        /// <summary>
        /// Creates new channel with custom event handler, authenticator and options
        /// </summary>
        /// <exception cref="OperationCanceledException">Thrown when channel limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a channel with same name</exception>
        public Channel CreateChannel(string name,
                                     IChannelAuthenticator authenticator,
                                     IChannelEventHandler eventHandler,
                                     IMessageDeliveryHandler deliveryHandler,
                                     ChannelOptions options)
        {
            if (!Filter.CheckNameEligibility(name))
                throw new InvalidOperationException("Invalid channel name");

            if (Options.ChannelLimit > 0 && _channels.Count >= Options.ChannelLimit)
                throw new OperationCanceledException("Channel limit is exceeded for the server");

            Channel channel = _channels.Find(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            if (channel != null)
                throw new DuplicateNameException("There is already a channel with same name: " + name);

            channel = new Channel(this, options, name, authenticator, eventHandler, deliveryHandler);
            _channels.Add(channel);

            if (eventHandler != null)
                _ = eventHandler.OnChannelCreated(channel);

            _ = OnChannelCreated.Trigger(channel);

            return channel;
        }

        /// <summary>
        /// Finds the channel by name
        /// </summary>
        public Channel FindChannel(string name)
        {
            Channel channel = _channels.Find(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            return channel;
        }

        /// <summary>
        /// Searches the channel, if channel could not be found, it will be created
        /// </summary>
        public Channel FindOrCreateChannel(string name)
        {
            lock (_findOrCreateChannelLocker)
            {
                Channel channel = FindChannel(name);
                if (channel == null)
                    channel = CreateChannel(name);

                return channel;
            }
        }

        /// <summary>
        /// Removes channel, destroys all queues in it
        /// </summary>
        public async Task RemoveChannel(string name)
        {
            Channel channel = FindChannel(name);
            if (channel == null)
                return;

            await RemoveChannel(channel);
        }

        /// <summary>
        /// Removes channel, destroys all queues in it
        /// </summary>
        public async Task RemoveChannel(Channel channel)
        {
            foreach (ChannelQueue queue in channel.QueuesClone)
                await channel.RemoveQueue(queue);

            _channels.Remove(channel);

            if (channel.EventHandler != null)
                await channel.EventHandler.OnChannelRemoved(channel);

            await channel.Destroy();

            _ = OnChannelRemoved.Trigger(channel);
        }

        #endregion

        #region Client Actions

        /// <summary>
        /// Adds new client to the server
        /// </summary>
        internal void AddClient(MqClient client)
        {
            _clients.Add(client);
            _ = OnClientConnected.Trigger(client);
        }

        /// <summary>
        /// Removes the client from the server
        /// </summary>
        internal async Task RemoveClient(MqClient client)
        {
            _clients.Remove(client);
            await client.LeaveFromAllChannels();
            _ = OnClientDisconnected.Trigger(client);
        }

        /// <summary>
        /// Finds client from unique id
        /// </summary>
        public MqClient FindClient(string uniqueId)
        {
            return _clients.Find(x => x.UniqueId == uniqueId);
        }

        /// <summary>
        /// Finds all connected client with specified name
        /// </summary>
        public List<MqClient> FindClientByName(string name)
        {
            return _clients.FindAll(x => x.Name == name);
        }

        /// <summary>
        /// Finds all connected client with specified type
        /// </summary>
        public List<MqClient> FindClientByType(string type)
        {
            return _clients.FindAll(x => x.Type == type);
        }

        /// <summary>
        /// Returns online clients
        /// </summary>
        public int GetOnlineClients()
        {
            return _clients.Count;
        }

        #endregion

        #region Router Actions

        /// <summary>
        /// Creates new Router and adds it to server routers.
        /// Throws exception if name is not eligible
        /// </summary>
        public IRouter AddRouter(string name, RouteMethod method)
        {
            if (!Filter.CheckNameEligibility(name))
                throw new InvalidOperationException("Invalid router name");

            if (_routers.Find(x => x.Name == name) != null)
                throw new DuplicateNameException();

            Router router = new Router(this, name, method);
            _routers.Add(router);
            return router;
        }

        /// <summary>
        /// Adds new router to server server routers
        /// Throws exception if name is not eligible
        /// </summary>
        public void AddRouter(IRouter router)
        {
            if (!Filter.CheckNameEligibility(router.Name))
                throw new InvalidOperationException("Invalid router name");

            if (_routers.Find(x => x.Name == router.Name) != null)
                throw new DuplicateNameException();

            _routers.Add(router);
        }

        /// <summary>
        /// Removes the router from server routers
        /// </summary>
        public void RemoveRouter(IRouter router)
        {
            _routers.Remove(router);
        }

        /// <summary>
        /// Finds router by it's name
        /// </summary>
        public IRouter FindRouter(string name)
        {
            return _routers.Find(x => x.Name == name);
        }

        #endregion
    }
}