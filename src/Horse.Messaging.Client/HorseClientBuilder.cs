using System;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Client
{
    /// <summary>
    /// Horse Client Builder
    /// </summary>
    public class HorseClientBuilder
    {
        #region Declaration

        private readonly HorseClient _client;
        private ModelTypeConfigurator _configurator;
        private readonly IServiceCollection _services;

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
        internal HorseClientBuilder(IServiceCollection services)
        {
            _services = services;
            _client = new HorseClient();
        }

        /// <summary>
        /// Builds new HmqStickyConnector with defined properties.
        /// </summary>
        public HorseClient Build()
        {
            return _client;
        }

        #endregion

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

        #region Direct Handlers

        public HorseClientBuilder AddTransientDirectHandler<THandler>() where THandler : class
        {
            if (_services == null)
                throw new NotSupportedException("Transient handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
            registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));

            return this;
        }

        public HorseClientBuilder AddScopedDirectHandler<THandler>() where THandler : class
        {
            if (_services == null)
                throw new NotSupportedException("Scoped handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
            registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));

            return this;
        }

        public HorseClientBuilder AddSingletonDirectHandler<THandler>() where THandler : class
        {
            DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
            if (_services == null)
                registrar.RegisterHandler(typeof(THandler));
            else
                registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));

            return this;
        }


        public HorseClientBuilder AddTransientDirectHandlers(params Type[] assemblyTypes)
        {
            if (_services == null)
                throw new NotSupportedException("Transient handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
            registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), assemblyTypes);
            return this;
        }

        public HorseClientBuilder AddScopedDirectHandlers(params Type[] assemblyTypes)
        {
            if (_services == null)
                throw new NotSupportedException("Scoped handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
            registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), assemblyTypes);

            return this;
        }

        public HorseClientBuilder AddSingletonDirectHandlers(params Type[] assemblyTypes)
        {
            DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
            if (_services == null)
                registrar.RegisterAssemblyHandlers(assemblyTypes);
            else
                registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), assemblyTypes);

            return this;
        }

        #endregion

        #region Channel Subscribers

        public HorseClientBuilder AddTransientChannelSubscriber<THandler>() where THandler : class
        {
            if (_services == null)
                throw new NotSupportedException("Transient handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
            registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));

            return this;
        }

        public HorseClientBuilder AddScopedChannelSubscriber<THandler>() where THandler : class
        {
            if (_services == null)
                throw new NotSupportedException("Scoped handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
            registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));

            return this;
        }

        public HorseClientBuilder AddSingletonChannelSubscriber<THandler>() where THandler : class
        {
            ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
            if (_services == null)
                registrar.RegisterHandler(typeof(THandler));
            else
                registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));

            return this;
        }


        public HorseClientBuilder AddTransientChannelSubscribers(params Type[] assemblyTypes)
        {
            if (_services == null)
                throw new NotSupportedException("Transient handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
            registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), assemblyTypes);
            return this;
        }

        public HorseClientBuilder AddScopedChannelSubscribers(params Type[] assemblyTypes)
        {
            if (_services == null)
                throw new NotSupportedException("Scoped handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
            registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), assemblyTypes);

            return this;
        }

        public HorseClientBuilder AddSingletonChannelSubscribers(params Type[] assemblyTypes)
        {
            ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
            if (_services == null)
                registrar.RegisterAssemblyHandlers(assemblyTypes);
            else
                registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), assemblyTypes);

            return this;
        }

        #endregion

        #region Queue Consumers

        public HorseClientBuilder AddTransientConsumer<TConsumer>() where TConsumer : class
        {
            if (_services == null)
                throw new NotSupportedException("Transient handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
            registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));

            return this;
        }

        public HorseClientBuilder AddScopedConsumer<TConsumer>() where TConsumer : class
        {
            if (_services == null)
                throw new NotSupportedException("Scoped handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
            registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));

            return this;
        }

        public HorseClientBuilder AddSingletonConsumer<TConsumer>() where TConsumer : class
        {
            QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
            if (_services == null)
                registrar.RegisterConsumer(typeof(TConsumer));
            else
                registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));

            return this;
        }


        public HorseClientBuilder AddTransientConsumers(params Type[] assemblyTypes)
        {
            if (_services == null)
                throw new NotSupportedException("Transient handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
            registrar.RegisterAssemblyConsumers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), assemblyTypes);
            return this;
        }

        public HorseClientBuilder AddScopedConsumers(params Type[] assemblyTypes)
        {
            if (_services == null)
                throw new NotSupportedException("Scoped handlers are not supported. " +
                                                "If you want to use transient direct receivers " +
                                                "Build HorseClient with IServiceCollection");

            QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
            registrar.RegisterAssemblyConsumers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), assemblyTypes);

            return this;
        }

        public HorseClientBuilder AddSingletonConsumers(params Type[] assemblyTypes)
        {
            QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
            if (_services == null)
                registrar.RegisterAssemblyConsumers(assemblyTypes);
            else
                registrar.RegisterAssemblyConsumers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), assemblyTypes);

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
    }
}