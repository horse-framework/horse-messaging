using System;
using System.Collections.Generic;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Client
{
    /// <summary>
    /// Horse Client Builder
    /// </summary>
    public class HorseClientBuilder
    {
        #region Fields

        private HorseClient _client;
        private ModelTypeConfigurator _configurator;

        internal readonly List<Tuple<ServiceLifetime, Type>> IndividualConsumers = new List<Tuple<ServiceLifetime, Type>>();
        internal readonly List<Tuple<ServiceLifetime, Type>> AssembyConsumers = new List<Tuple<ServiceLifetime, Type>>();

        private readonly object _serviceContainer;

        #endregion

        /// <summary>
        /// Creates Horse Connector Builder without IOC implementation
        /// </summary>
        public HorseClientBuilder()
        {
            _client = new HorseClient();
        }

        /// <summary>
        /// Creates Horse Connector Builder with IOC implementation
        /// </summary>
        internal HorseClientBuilder(object serviceContainer)
        {
            _serviceContainer = serviceContainer;
            _client = new HorseClient();
        }

        #region Client Info

        /// <summary>
        /// Sets client Id. It must be unique.
        /// If another client with same id is already connected to server,
        /// Server will generate new id for this connector
        /// </summary>
        public HorseClientBuilder SetClientId(string id)
        {
            _client.SetClientId(id);
            return this;
        }

        /// <summary>
        /// Client name
        /// </summary>
        public HorseClientBuilder SetClientName(string name)
        {
            _client.SetClientName(name);
            return this;
        }

        /// <summary>
        /// Client type
        /// </summary>
        public HorseClientBuilder SetClientType(string type)
        {
            _client.SetClientType(type);
            return this;
        }

        /// <summary>
        /// Client token for server side authentication and authorization
        /// </summary>
        public HorseClientBuilder SetClientToken(string token)
        {
            _client.SetClientToken(token);
            return this;
        }

        #endregion

        #region Connection

        /// <summary>
        /// Adds new host to connect
        /// </summary>
        public HorseClientBuilder SetHost(string hostname)
        {
            _client.RemoteHost = hostname;
            return this;
        }

        /// <summary>
        /// Sets reconnection interval if disconnects. Default is 1000 milliseconds.
        /// </summary>
        public HorseClientBuilder SetReconnectWait(TimeSpan value)
        {
            _client.ReconnectWait = value;
            return this;
        }

        /// <summary>
        /// If true, connector subscribes all consuming queues automatically right after connection established.
        /// If false, you need to subscribe manually
        /// Default is true.
        /// </summary>
        public HorseClientBuilder AutoSubscribe(bool value)
        {
            _client.AutoSubscribe = value;
            return this;
        }

        /// <summary>
        /// If true, disconnected when any of auto subscribe request fails.
        /// Default is true.
        /// </summary>
        public HorseClientBuilder DisconnectionOnAutoSubscribeFailure(bool value)
        {
            _client.DisconnectionOnAutoJoinFailure = value;
            return this;
        }

        #endregion

        #region Serializers

        /// <summary>
        /// Uses Newtonsoft library for JSON serializations
        /// </summary>
        public HorseClientBuilder UseNewtonsoftJsonSerializer(Newtonsoft.Json.JsonSerializerSettings settings = null)
        {
            _client.MessageSerializer = new NewtonsoftContentSerializer(settings);
            return this;
        }

        /// <summary>
        /// Uses System.Text.Json library for JSON serializations
        /// </summary>
        public HorseClientBuilder UseSystemJsonSerializer(System.Text.Json.JsonSerializerOptions options = null)
        {
            _client.MessageSerializer = new SystemJsonContentSerializer(options);
            return this;
        }

        /// <summary>
        /// Uses custom serializer
        /// </summary>
        public HorseClientBuilder UseCustomSerializer(IMessageContentSerializer serializer)
        {
            _client.MessageSerializer = serializer;
            return this;
        }

        #endregion

        #region Consumers

        /// <summary>
        /// Registers new transient consumer
        /// </summary>
        public HorseClientBuilder AddTransientConsumer<TConsumer>() where TConsumer : class
        {
            _individualConsumers.Add(new Tuple<ServiceLifetime, Type>(ServiceLifetime.Transient, typeof(TConsumer)));
            return this;
        }

        /// <summary>
        /// Registers new scoped consumer
        /// </summary>
        public HorseClientBuilder AddScopedConsumer<TConsumer>() where TConsumer : class
        {
            _individualConsumers.Add(new Tuple<ServiceLifetime, Type>(ServiceLifetime.Scoped, typeof(TConsumer)));
            return this;
        }

        /// <summary>
        /// Registers new singleton consumer
        /// </summary>
        public HorseClientBuilder AddSingletonConsumer<TConsumer>() where TConsumer : class
        {
            _individualConsumers.Add(new Tuple<ServiceLifetime, Type>(ServiceLifetime.Singleton, typeof(TConsumer)));
            return this;
        }

        /// <summary>
        /// Registers all consumers types with transient lifetime in type assemblies
        /// </summary>
        public HorseClientBuilder AddTransientConsumers(params Type[] assemblyTypes)
        {
            foreach (Type type in assemblyTypes)
                _assembyConsumers.Add(new Tuple<ServiceLifetime, Type>(ServiceLifetime.Transient, type));

            return this;
        }

        /// <summary>
        /// Registers all consumers types with scoped lifetime in type assemblies
        /// </summary>
        public HorseClientBuilder AddScopedConsumers(params Type[] assemblyTypes)
        {
            foreach (Type type in assemblyTypes)
                _assembyConsumers.Add(new Tuple<ServiceLifetime, Type>(ServiceLifetime.Scoped, type));

            return this;
        }

        /// <summary>
        /// Registers all consumers types with singleton lifetime in type assemblies
        /// </summary>
        public HorseClientBuilder AddSingletonConsumers(params Type[] assemblyTypes)
        {
            foreach (Type type in assemblyTypes)
                _assembyConsumers.Add(new Tuple<ServiceLifetime, Type>(ServiceLifetime.Singleton, type));

            return this;
        }

        #endregion

        #region Options

        /// <summary>
        /// Sets default configuration for all model and consumer types.
        /// The configuration options can be overwritten with attributes.
        /// </summary>
        public HorseClientBuilder ConfigureModels(Action<ModelTypeConfigurator> cfg)
        {
            _configurator = new ModelTypeConfigurator();
            cfg(_configurator);
            return this;
        }

        #endregion

        #region Events

        /// <summary>
        /// Action for connected events
        /// </summary>
        public HorseClientBuilder OnConnected(Action<HorseClient> action)
        {
            _client.ConnectedAction = action;
            return this;
        }

        /// <summary>
        /// Action for disconnected events
        /// </summary>
        public HorseClientBuilder OnDisconnected(Action<HorseClient> action)
        {
            _client.DisconnectedAction = action;
            return this;
        }

        /// <summary>
        /// Action for errors
        /// </summary>
        public HorseClientBuilder OnError(Action<Exception> action)
        {
            _client.ErrorAction = action;
            return this;
        }

        #endregion

        #region Build - Dispose

        /// <summary>
        /// Builds new HmqStickyConnector with defined properties.
        /// </summary>
        public HorseClient Build()
        {
            if (_client != null)
            {
                ConfigureConnector(_connector);
                if (_serviceContainer == null)
                    RegisterConsumers(_connector);
            }

            return _client;
        }

        /// <summary>
        /// Registers all consumers.
        /// This method is called if implementation is done without ioc container.
        /// </summary>
        private void RegisterConsumers(HmqStickyConnector connector)
        {
            foreach (Tuple<ServiceLifetime, Type> pair in _assembyConsumers)
                connector.Observer.RegisterAssemblyConsumers(pair.Item2);

            foreach (Tuple<ServiceLifetime, Type> pair in _individualConsumers)
                connector.Observer.RegisterConsumer(pair.Item2);
        }

        /// <summary>
        /// Applies configurations on connector
        /// </summary>
        private void ConfigureConnector(HorseClient client)
        {
            connector.Observer.Configurator = _configurator;
            connector.AutoSubscribe = _autoSubscribe;
            connector.DisconnectionOnAutoJoinFailure = _disconnectOnSubscribeFailure;
            if (_contentSerializer != null)
                connector.ContentSerializer = _contentSerializer;

            foreach (string host in _hosts)
                connector.AddHost(host);

            if (_connected != null)
                connector.Connected += new ConnectionEventMapper(connector, _connected).Action;

            if (_disconnected != null)
                connector.Disconnected += new ConnectionEventMapper(connector, _disconnected).Action;

            if (_error != null)
                connector.ExceptionThrown += new ExceptionEventMapper(connector, _error).Action;
        }

        #endregion
    }
}