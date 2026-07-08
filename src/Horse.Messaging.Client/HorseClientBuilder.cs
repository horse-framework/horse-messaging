using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Client.Interceptors;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: InternalsVisibleTo("Horse.Messaging.Extensions.Client")]

namespace Horse.Messaging.Client;

/// <inheritdoc />
public class HorseClientBuilder<TIdentifier> : HorseClientBuilder
{
    /// <summary>
    /// Creates Horse Connector Builder without IOC implementation
    /// </summary>
    public HorseClientBuilder() : base(new HorseClient<TIdentifier>())
    {
    }

    /// <summary>
    /// Creates Horse Connector Builder with IOC implementation
    /// </summary>
    public HorseClientBuilder(IServiceCollection services) : base(services, new HorseClient<TIdentifier>())
    {
    }

    /// <summary>
    /// Builds new HorseClient with defined properties.
    /// </summary>
    public override HorseClient<TIdentifier> Build()
    {
        return (HorseClient<TIdentifier>)base.Build();
    }
}

/// <summary>
/// Horse Client Builder
/// </summary>
/// 
public class HorseClientBuilder
{
    #region Declaration

    private readonly HorseClient _client;
    private readonly IServiceCollection _services;

    /// <summary>
    /// Service collection
    /// </summary>
    internal IServiceCollection Services => _services;

    /// <summary>
    /// Service key for keyed services. Null if not using keyed services.
    /// </summary>
    internal string ServiceKey { get; set; }

    /// <summary>
    /// If true, logs errors with Microsoft.Extensions.Logging
    /// </summary>
    public bool UseQueueErrorLogging
    {
        set => _client.Queue.LogErrors = value;
    }

    /// <summary>
    /// Creates Horse Connector Builder without IOC implementation
    /// </summary>
    public HorseClientBuilder()
    {
        _services = new ServiceCollection();
        _client = new HorseClient();
    }

    /// <summary>
    /// Creates Horse Connector Builder with IOC implementation
    /// </summary>
    public HorseClientBuilder(IServiceCollection services)
    {
        _services = services;
        _client = new HorseClient();
    }

    internal HorseClientBuilder(HorseClient client)
    {
        _services = new ServiceCollection();
        _client = client;
    }

    internal HorseClientBuilder(IServiceCollection services, HorseClient client)
    {
        _services = services;
        _client = client;
    }

    /// <summary>
    /// Builds new HorseClient with defined properties.
    /// </summary>
    public virtual HorseClient Build()
    {
        if (_services != null && _client.Provider == null)
            _client.Provider = _services.BuildServiceProvider();

        return _client;
    }

    #endregion

    #region Client Info

    /// <summary>
    /// Gets premature client object
    /// </summary>
    /// <returns></returns>
    public HorseClient GetClient() => _client;

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
    [Obsolete("Use AddHost method instead of this")]
    public HorseClientBuilder SetHost(string hostname)
    {
        return AddHost(hostname);
    }

    /// <summary>
    /// Adds new host to connect
    /// </summary>
    public HorseClientBuilder AddHost(string hostname)
    {
        _client.AddHost(hostname);
        return this;
    }

    /// <summary>
    /// Sets reconnection interval if disconnects. Default is 3000 milliseconds.
    /// </summary>
    public HorseClientBuilder SetReconnectWait(TimeSpan value)
    {
        _client.ReconnectWait = value;
        return this;
    }

    /// <summary>
    /// Sets reconnection interval if disconnects. Default is 30 seconds.
    /// </summary>
    public HorseClientBuilder SetResponseTimeout(TimeSpan value)
    {
        _client.ResponseTimeout = value;
        return this;
    }

    /// <summary>
    /// If true, connector subscribes all consuming queues automatically right after connection established.
    /// If false, you need to subscribe manually
    /// Default value is true.
    /// </summary>
    public HorseClientBuilder AutoSubscribe(bool value)
    {
        _client.AutoSubscribe = value;
        return this;
    }

    /// <summary>
    /// Determines if a client should automatically acknowledge a message.
    /// Default value is false.
    /// </summary>
    public HorseClientBuilder AutoAcknowledge(bool value)
    {
        _client.AutoAcknowledge = value;
        return this;
    }

    /// <summary>
    /// If true, disconnected when any of auto subscribe request fails.
    /// Default value is true.
    /// </summary>
    public HorseClientBuilder DisconnectionOnAutoSubscribeFailure(bool value)
    {
        _client.DisconnectionOnAutoJoinFailure = value;
        return this;
    }

    /// <summary>
    /// Uses switching protocol. Horse Protocol streams over that protocol. 
    /// </summary>
    public HorseClientBuilder UseSwitchingProtocol(ISwitchingProtocol protocol)
    {
        _client.SwitchingProtocol = protocol;
        return this;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="minWait"></param>
    /// <param name="maxWait"></param>
    /// <param name="shuttingDownAction"></param>
    /// <returns></returns>
    public HorseClientBuilder UseGracefulShutdown(TimeSpan minWait, TimeSpan maxWait, Func<IServiceProvider, Task> shuttingDownAction = null)
    {
        _client.GracefulShutdownOptions = new GracefulShutdownOptions
        {
            MinWait = minWait,
            MaxWait = maxWait,
            ShuttingDownActionWithProvider = shuttingDownAction
        };
        return this;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="minWait"></param>
    /// <param name="maxWait"></param>
    /// <param name="shuttingDownAction"></param>
    /// <returns></returns>
    public HorseClientBuilder UseGracefulShutdown(TimeSpan minWait, TimeSpan maxWait, Func<Task> shuttingDownAction = null)
    {
        _client.GracefulShutdownOptions = new GracefulShutdownOptions
        {
            MinWait = minWait,
            MaxWait = maxWait,
            ShuttingDownAction = shuttingDownAction
        };
        return this;
    }

    #endregion

    #region Serializers

    /// <summary>
    /// Uses Newtonsoft library for JSON serializations
    /// </summary>
    [Obsolete(
        "Newtonsoft.Json support has dropped. If you still want to use Newtonsoft.Json, please create a custom serializer using the IMessageContentSerializer interface and call UseCustomSerializer(). An example can be found here: https://github.com/horse-framework/horse-messaging/blob/v6.3/src/Horse.Messaging.Client/NewtonsoftContentSerializer.cs",
        true)]
    public HorseClientBuilder UseNewtonsoftJsonSerializer()
    {
        return this;
    }

    /// <summary>
    /// Uses System.Text.Json library for JSON serializations.
    /// This is the default serializer.
    /// </summary>
    public HorseClientBuilder UseSystemJsonSerializer(System.Text.Json.JsonSerializerOptions options = null)
    {
        _client.MessageSerializer = new SystemJsonContentSerializer(options);
        return this;
    }

    /// <summary>
    /// Overrides the default serializer with a custom one.
    /// The default serializer is <see cref="SystemJsonContentSerializer"/>.
    /// </summary>
    public HorseClientBuilder UseCustomSerializer(IMessageContentSerializer serializer)
    {
        _client.MessageSerializer = serializer;
        return this;
    }

    #endregion

    #region Direct Handlers

    /// <summary>
    /// Adds a direct handler with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientDirectHandler<THandler>() where THandler : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient direct receivers " +
                                            "Build HorseClient with IServiceCollection");

        DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
        registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));
        _services.AddTransient<THandler>();
        return this;
    }

    /// <summary>
    /// Adds a direct handler with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedDirectHandler<THandler>() where THandler : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped direct receivers " +
                                            "Build HorseClient with IServiceCollection");

        DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
        registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));
        _services.AddScoped<THandler>();
        return this;
    }

    /// <summary>
    /// Adds a direct handler with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonDirectHandler<THandler>() where THandler : class
    {
        DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
        if (_services == null)
            registrar.RegisterHandler(typeof(THandler));
        else
        {
            registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));
            _services.AddSingleton<THandler>();
        }

        return this;
    }

    /// <summary>
    /// Adds all direct handler types in specified assemblies with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientDirectHandlers(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient direct receivers " +
                                            "Build HorseClient with IServiceCollection");

        DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
        IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), assemblyTypes);
        foreach (Type type in types) _services.AddTransient(type);
        return this;
    }

    /// <summary>
    /// Adds all direct handler types in specified assemblies with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedDirectHandlers(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped direct receivers " +
                                            "Build HorseClient with IServiceCollection");

        DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
        IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), assemblyTypes);
        foreach (Type type in types) _services.AddScoped(type);
        return this;
    }

    /// <summary>
    /// Adds all direct handler types in specified assemblies with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonDirectHandlers(params Type[] assemblyTypes)
    {
        DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(_client.Direct);
        if (_services == null)
            registrar.RegisterAssemblyHandlers(assemblyTypes);
        else
        {
            IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), assemblyTypes);
            foreach (Type type in types) _services.AddSingleton(type);
        }

        return this;
    }

    #endregion

    #region Channel Subscribers

    /// <summary>
    /// Adds a channel subscriber with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientChannelSubscriber<THandler>() where THandler : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient channel handlers " +
                                            "Build HorseClient with IServiceCollection");

        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));
        _services.AddTransient<THandler>();
        return this;
    }

    /// <summary>
    /// Adds a channel subscriber with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientChannelSubscriber<THandler, TModel>(Func<HorseMessage, TModel, bool> filter)
        where THandler : class, IChannelSubscriber<TModel>
        where TModel : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient channel handlers " +
                                            "Build HorseClient with IServiceCollection");

        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), (h, m) => filter(h, (TModel)m));
        _services.AddTransient<THandler>();
        return this;
    }

    /// <summary>
    /// Adds a channel subscriber with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedChannelSubscriber<THandler>() where THandler : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped channel handlers " +
                                            "Build HorseClient with IServiceCollection");

        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));
        _services.AddScoped<THandler>();
        return this;
    }

    /// <summary>
    /// Adds a channel subscriber with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedChannelSubscriber<THandler, TModel>(Func<HorseMessage, TModel, bool> filter)
        where THandler : class, IChannelSubscriber<TModel>
        where TModel : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped channel handlers " +
                                            "Build HorseClient with IServiceCollection");

        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), (h, m) => filter(h, (TModel)m));
        _services.AddScoped<THandler>();
        return this;
    }

    /// <summary>
    /// Adds a channel subscriber with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonChannelSubscriber<THandler>() where THandler : class
    {
        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        if (_services == null)
            registrar.RegisterHandler(typeof(THandler));
        else
        {
            registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));
            _services.AddSingleton<THandler>();
        }

        return this;
    }

    /// <summary>
    /// Adds a channel subscriber with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonChannelSubscriber<THandler, TModel>(Func<HorseMessage, TModel, bool> filter)
        where THandler : class, IChannelSubscriber<TModel>
        where TModel : class
    {
        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        if (_services == null)
            registrar.RegisterHandler(typeof(THandler));
        else
        {
            registrar.RegisterHandler(typeof(THandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), (h, m) => filter(h, (TModel)m));
            _services.AddSingleton<THandler>();
        }

        return this;
    }

    /// <summary>
    /// Adds all channel susbcriber types in specified assemblies with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientChannelSubscribers(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient channel handlers " +
                                            "Build HorseClient with IServiceCollection");

        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), assemblyTypes);
        foreach (Type type in types) _services.AddTransient(type);
        return this;
    }

    /// <summary>
    /// Adds all channel susbcriber types in specified assemblies with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedChannelSubscribers(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped channel handlers " +
                                            "Build HorseClient with IServiceCollection");

        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), assemblyTypes);
        foreach (Type type in types) _services.AddScoped(type);
        return this;
    }

    /// <summary>
    /// Adds all channel susbcriber types in specified assemblies with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonChannelSubscribers(params Type[] assemblyTypes)
    {
        ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(_client.Channel);
        if (_services == null)
            registrar.RegisterAssemblyHandlers(assemblyTypes);
        else
        {
            IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), assemblyTypes);
            foreach (Type type in types) _services.AddSingleton(type);
        }

        return this;
    }

    #endregion

    #region Queue Consumers

    /// <summary>
    /// Adds a queue consumer with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientConsumer<TConsumer>(Action<QueueConfigBuilder> builder = null) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        QueueTypeDescriptor descriptor = BuildConsumerDescriptor(builder, ServiceLifetime.Transient);
        registrar.RegisterConsumer(typeof(TConsumer), descriptor, () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));
        _services.AddTransient<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with transient life time and subscribes to a partitioned queue.
    /// </summary>
    /// <param name="partitionLabel">
    /// Routing label sent as <c>Partition-Label</c> header.
    /// Pass <c>null</c> or empty string for label-less (round-robin) partitioned subscribe.
    /// </param>
    /// <param name="maxPartitions">
    /// Maximum partitions forwarded as <c>Partition-Limit</c> header for auto-create.
    /// <c>null</c> = not set (server default), <c>0</c> = unlimited.
    /// </param>
    /// <param name="subscribersPerPartition">
    /// Max subscribers per partition for auto-create. <c>null</c> = not set (server default).
    /// </param>
    public HorseClientBuilder AddTransientConsumer<TConsumer>(string partitionLabel, int? maxPartitions = null, int? subscribersPerPartition = null) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), partitionLabel, maxPartitions, subscribersPerPartition);
        _services.AddTransient<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with transient life time, overrides the queue name,
    /// and optionally subscribes with a partition label.
    /// </summary>
    /// <param name="queueName">Overrides the queue name resolved from attributes or model type.</param>
    /// <param name="partitionLabel">Optional partition label. Pass null for no partition.</param>
    public HorseClientBuilder AddTransientConsumer<TConsumer>(string queueName, string partitionLabel) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), queueName, partitionLabel);
        _services.AddTransient<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with transient life time, transforms the queue name resolved from attributes,
    /// and optionally enters the partition worker pool for auto-assignment.
    /// </summary>
    /// <param name="queueNameTransform">
    /// Receives the original queue name (from <c>[QueueName]</c> attribute or model type) and returns the final queue name.
    /// Example: <c>name => $"{name}-Free"</c>
    /// </param>
    /// <param name="enterWorkerPool">
    /// When true, the consumer subscribes as a partitioned worker without a label.
    /// The server will dynamically assign it to newly created partitions.
    /// </param>
    public HorseClientBuilder AddTransientConsumer<TConsumer>(Func<string, string> queueNameTransform, bool enterWorkerPool = false) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), queueNameTransform, enterWorkerPool);
        _services.AddTransient<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with transient life time, transforms the queue name resolved from attributes,
    /// and subscribes with the given partition label.
    /// </summary>
    /// <param name="queueNameTransform">
    /// Receives the original queue name (from <c>[QueueName]</c> attribute or model type) and returns the final queue name.
    /// Example: <c>name => $"{name}-Free"</c>
    /// </param>
    /// <param name="partitionLabel">
    /// The partition label to subscribe with. Cannot be null or empty.
    /// To enter the worker pool without a label, use the <c>enterWorkerPool</c> overload.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="partitionLabel"/> is null or empty.</exception>
    public HorseClientBuilder AddTransientConsumer<TConsumer>(Func<string, string> queueNameTransform, string partitionLabel) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), queueNameTransform, partitionLabel);
        _services.AddTransient<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedConsumer<TConsumer>(Action<QueueConfigBuilder> builder = null) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        QueueTypeDescriptor descriptor = BuildConsumerDescriptor(builder, ServiceLifetime.Scoped);
        registrar.RegisterConsumer(typeof(TConsumer), descriptor, () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));
        _services.AddScoped<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with scoped life time and subscribes to a partitioned queue.
    /// </summary>
    /// <param name="partitionLabel">
    /// Routing label sent as <c>Partition-Label</c> header.
    /// Pass <c>null</c> or empty string for label-less partitioned subscribe.
    /// </param>
    /// <param name="maxPartitions">Maximum partitions for auto-create. <c>null</c> = not set (server default), <c>0</c> = unlimited.</param>
    /// <param name="subscribersPerPartition">Max subscribers per partition. <c>null</c> = not set (server default).</param>
    public HorseClientBuilder AddScopedConsumer<TConsumer>(string partitionLabel, int? maxPartitions = null, int? subscribersPerPartition = null) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), partitionLabel, maxPartitions, subscribersPerPartition);
        _services.AddScoped<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with scoped life time, overrides the queue name,
    /// and optionally subscribes with a partition label.
    /// </summary>
    /// <param name="queueName">Overrides the queue name resolved from attributes or model type.</param>
    /// <param name="partitionLabel">Optional partition label. Pass null for no partition.</param>
    public HorseClientBuilder AddScopedConsumer<TConsumer>(string queueName, string partitionLabel) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), queueName, partitionLabel);
        _services.AddScoped<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with scoped life time, transforms the queue name resolved from attributes,
    /// and optionally enters the partition worker pool for auto-assignment.
    /// </summary>
    /// <param name="queueNameTransform">
    /// Receives the original queue name (from <c>[QueueName]</c> attribute or model type) and returns the final queue name.
    /// Example: <c>name => $"{name}-Free"</c>
    /// </param>
    /// <param name="enterWorkerPool">
    /// When true, the consumer subscribes as a partitioned worker without a label.
    /// The server will dynamically assign it to newly created partitions.
    /// </param>
    public HorseClientBuilder AddScopedConsumer<TConsumer>(Func<string, string> queueNameTransform, bool enterWorkerPool = false) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), queueNameTransform, enterWorkerPool);
        _services.AddScoped<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with scoped life time, transforms the queue name resolved from attributes,
    /// and subscribes with the given partition label.
    /// </summary>
    /// <param name="queueNameTransform">
    /// Receives the original queue name (from <c>[QueueName]</c> attribute or model type) and returns the final queue name.
    /// Example: <c>name => $"{name}-Free"</c>
    /// </param>
    /// <param name="partitionLabel">
    /// The partition label to subscribe with. Cannot be null or empty.
    /// To enter the worker pool without a label, use the <c>enterWorkerPool</c> overload.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="partitionLabel"/> is null or empty.</exception>
    public HorseClientBuilder AddScopedConsumer<TConsumer>(Func<string, string> queueNameTransform, string partitionLabel) where TConsumer : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), queueNameTransform, partitionLabel);
        _services.AddScoped<TConsumer>();
        return this;
    }

    /// <summary>
    /// Adds a queue consumer with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonConsumer<TConsumer>(Action<QueueConfigBuilder> builder = null) where TConsumer : class
    {
        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        QueueTypeDescriptor descriptor = BuildConsumerDescriptor(builder, ServiceLifetime.Singleton);

        if (_services == null)
            registrar.RegisterConsumer(typeof(TConsumer), descriptor);
        else
        {
            registrar.RegisterConsumer(typeof(TConsumer), descriptor, () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));
            _services.AddSingleton<TConsumer>();
        }

        return this;
    }

    private QueueTypeDescriptor BuildConsumerDescriptor(Action<QueueConfigBuilder> builder, ServiceLifetime lifetime)
    {
        if (builder == null)
            return null;

        QueueConfigBuilder config = new QueueConfigBuilder();
        builder(config);
        RegisterBuilderInterceptors(config, lifetime);
        return config.Build();
    }

    private QueueTypeDescriptor BuildConsumerDescriptor(Type consumerType, Action<QueueConfigBuilder> builder, ServiceLifetime lifetime)
    {
        if (builder == null)
            return null;

        QueueConfigBuilder config = new QueueConfigBuilder();
        config.ConsumerType = consumerType;

        Type queueConsumerInterface = consumerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueueConsumer<>));

        if (queueConsumerInterface != null)
            config.ModelType = queueConsumerInterface.GetGenericArguments()[0];

        builder(config);
        RegisterBuilderInterceptors(config, lifetime);
        return config.Build();
    }

    private void RegisterBuilderInterceptors(QueueConfigBuilder config, ServiceLifetime lifetime)
    {
        if (_services == null || config.Interceptors.Count == 0)
            return;

        foreach (Type interceptorType in config.Interceptors.Select(x => x.InterceptorType).Distinct())
            _services.TryAdd(new ServiceDescriptor(interceptorType, interceptorType, lifetime));
    }

    private static IEnumerable<Type> FindAssemblyQueueConsumers(params Type[] assemblyTypes)
    {
        foreach (Type assemblyType in assemblyTypes)
        {
            foreach (Type type in assemblyType.Assembly.GetTypes())
            {
                if (type.IsInterface || type.IsAbstract)
                    continue;

                if (type.GetInterfaces().Any(m => m.IsGenericType && m.GetGenericTypeDefinition() == typeof(IQueueConsumer<>)))
                    yield return type;
            }
        }
    }

    /// <summary>
    /// Adds a queue consumer with singleton life time and subscribes to a partitioned queue.
    /// </summary>
    /// <param name="partitionLabel">
    /// Routing label sent as <c>Partition-Label</c> header.
    /// Pass <c>null</c> or empty string for label-less partitioned subscribe.
    /// </param>
    /// <param name="maxPartitions">Maximum partitions for auto-create. <c>null</c> = not set (server default), <c>0</c> = unlimited.</param>
    /// <param name="subscribersPerPartition">Max subscribers per partition. <c>null</c> = not set (server default).</param>
    public HorseClientBuilder AddSingletonConsumer<TConsumer>(string partitionLabel, int? maxPartitions = null, int? subscribersPerPartition = null) where TConsumer : class
    {
        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        if (_services == null)
            registrar.RegisterConsumer(typeof(TConsumer), null, partitionLabel, maxPartitions, subscribersPerPartition);
        else
        {
            registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), partitionLabel, maxPartitions, subscribersPerPartition);
            _services.AddSingleton<TConsumer>();
        }

        return this;
    }

    /// <summary>
    /// Adds a queue consumer with singleton life time, overrides the queue name,
    /// and optionally subscribes with a partition label.
    /// </summary>
    /// <param name="queueName">Overrides the queue name resolved from attributes or model type.</param>
    /// <param name="partitionLabel">Optional partition label. Pass null for no partition.</param>
    public HorseClientBuilder AddSingletonConsumer<TConsumer>(string queueName, string partitionLabel) where TConsumer : class
    {
        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        if (_services == null)
            registrar.RegisterConsumer(typeof(TConsumer), null, queueName, partitionLabel);
        else
        {
            registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), queueName, partitionLabel);
            _services.AddSingleton<TConsumer>();
        }

        return this;
    }

    /// <summary>
    /// Adds a queue consumer with singleton life time, transforms the queue name resolved from attributes,
    /// and optionally enters the partition worker pool for auto-assignment.
    /// </summary>
    /// <param name="queueNameTransform">
    /// Receives the original queue name (from <c>[QueueName]</c> attribute or model type) and returns the final queue name.
    /// Example: <c>name => $"{name}-Free"</c>
    /// </param>
    /// <param name="enterWorkerPool">
    /// When true, the consumer subscribes as a partitioned worker without a label.
    /// The server will dynamically assign it to newly created partitions.
    /// </param>
    public HorseClientBuilder AddSingletonConsumer<TConsumer>(Func<string, string> queueNameTransform, bool enterWorkerPool = false) where TConsumer : class
    {
        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        if (_services == null)
            registrar.RegisterConsumer(typeof(TConsumer), null, queueNameTransform, enterWorkerPool);
        else
        {
            registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), queueNameTransform, enterWorkerPool);
            _services.AddSingleton<TConsumer>();
        }

        return this;
    }

    /// <summary>
    /// Adds a queue consumer with singleton life time, transforms the queue name resolved from attributes,
    /// and subscribes with the given partition label.
    /// </summary>
    /// <param name="queueNameTransform">
    /// Receives the original queue name (from <c>[QueueName]</c> attribute or model type) and returns the final queue name.
    /// Example: <c>name => $"{name}-Free"</c>
    /// </param>
    /// <param name="partitionLabel">
    /// The partition label to subscribe with. Cannot be null or empty.
    /// To enter the worker pool without a label, use the <c>enterWorkerPool</c> overload.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="partitionLabel"/> is null or empty.</exception>
    public HorseClientBuilder AddSingletonConsumer<TConsumer>(Func<string, string> queueNameTransform, string partitionLabel) where TConsumer : class
    {
        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        if (_services == null)
            registrar.RegisterConsumer(typeof(TConsumer), null, queueNameTransform, partitionLabel);
        else
        {
            registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), queueNameTransform, partitionLabel);
            _services.AddSingleton<TConsumer>();
        }

        return this;
    }

    /// <summary>
    /// Adds all queue consumer types in specified assemblies with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientConsumers(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        IEnumerable<Type> types = registrar.RegisterAssemblyConsumers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), assemblyTypes);
        foreach (Type type in types) _services.AddTransient(type);
        return this;
    }

    /// <summary>
    /// Adds all queue consumer types in specified assemblies with transient life time and per-consumer builder configuration.
    /// </summary>
    public HorseClientBuilder AddTransientConsumers(Action<QueueConfigBuilder> builder, params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);

        foreach (Type consumerType in FindAssemblyQueueConsumers(assemblyTypes))
        {
            QueueTypeDescriptor descriptor = BuildConsumerDescriptor(consumerType, builder, ServiceLifetime.Transient);
            registrar.RegisterConsumer(consumerType, descriptor, () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));
            _services.AddTransient(consumerType);
        }

        return this;
    }

    /// <summary>
    /// Adds all queue consumer types in specified assemblies with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedConsumers(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        IEnumerable<Type> types = registrar.RegisterAssemblyConsumers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), assemblyTypes);
        foreach (Type type in types) _services.AddScoped(type);
        return this;
    }

    /// <summary>
    /// Adds all queue consumer types in specified assemblies with scoped life time and per-consumer builder configuration.
    /// </summary>
    public HorseClientBuilder AddScopedConsumers(Action<QueueConfigBuilder> builder, params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped queue consumers " +
                                            "Build HorseClient with IServiceCollection");

        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);

        foreach (Type consumerType in FindAssemblyQueueConsumers(assemblyTypes))
        {
            QueueTypeDescriptor descriptor = BuildConsumerDescriptor(consumerType, builder, ServiceLifetime.Scoped);
            registrar.RegisterConsumer(consumerType, descriptor, () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));
            _services.AddScoped(consumerType);
        }

        return this;
    }

    /// <summary>
    /// Adds all queue consumer types in specified assemblies with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonConsumers(params Type[] assemblyTypes)
    {
        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
        if (_services == null)
            registrar.RegisterAssemblyConsumers(assemblyTypes);
        else
        {
            IEnumerable<Type> types = registrar.RegisterAssemblyConsumers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), assemblyTypes);
            foreach (Type type in types) _services.AddSingleton(type);
        }

        return this;
    }

    /// <summary>
    /// Adds all queue consumer types in specified assemblies with singleton life time and per-consumer builder configuration.
    /// </summary>
    public HorseClientBuilder AddSingletonConsumers(Action<QueueConfigBuilder> builder, params Type[] assemblyTypes)
    {
        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);

        foreach (Type consumerType in FindAssemblyQueueConsumers(assemblyTypes))
        {
            QueueTypeDescriptor descriptor = BuildConsumerDescriptor(consumerType, builder, ServiceLifetime.Singleton);

            if (_services == null)
                registrar.RegisterConsumer(consumerType, descriptor);
            else
            {
                registrar.RegisterConsumer(consumerType, descriptor, () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));
                _services.AddSingleton(consumerType);
            }
        }

        return this;
    }

    #endregion

    #region Interceptors

    /// <summary>
    /// Registers new transient interceptor
    /// </summary>
    public HorseClientBuilder AddTransientInterceptor<TInterceptor>() where TInterceptor : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient interceptors " +
                                            "Build HorseClient with IServiceCollection");

        _services.AddTransient<TInterceptor>();
        return this;
    }

    /// <summary>
    /// Registers new scoped interceptor
    /// </summary>
    public HorseClientBuilder AddScopedInterceptor<TInterceptor>() where TInterceptor : class
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped interceptors " +
                                            "Build HorseClient with IServiceCollection");

        _services.AddScoped<TInterceptor>();
        return this;
    }

    /// <summary>
    /// Registers new singleton interceptor
    /// </summary>
    public HorseClientBuilder AddSingletonInterceptor<TInterceptor>() where TInterceptor : class
    {
        if (_services is null) return this;
        _services.AddSingleton<TInterceptor>();
        return this;
    }

    /// <summary>
    /// Registers all interceptors types with transient lifetime in type assemblies
    /// </summary>
    public HorseClientBuilder AddTransientInterceptors(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient interceptors " +
                                            "Build HorseClient with IServiceCollection");

        IEnumerable<Type> types = assemblyTypes.Where(m => !m.IsAbstract && !m.IsInterface && typeof(IHorseInterceptor).IsAssignableFrom(m));
        foreach (Type type in types) _services.AddTransient(type);
        return this;
    }

    /// <summary>
    /// Registers all interceptors types with scoped lifetime in type assemblies
    /// </summary>
    public HorseClientBuilder AddScopedInterceptors(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped interceptors " +
                                            "Build HorseClient with IServiceCollection");

        IEnumerable<Type> types = assemblyTypes.Where(m => !m.IsAbstract && !m.IsInterface && typeof(IHorseInterceptor).IsAssignableFrom(m));
        foreach (Type type in types) _services.AddScoped(type);
        return this;
    }

    /// <summary>
    /// Registers all interceptors types with singleton lifetime in type assemblies
    /// </summary>
    public HorseClientBuilder AddSingletonInterceptors(params Type[] assemblyTypes)
    {
        if (_services is null) return this;
        IEnumerable<Type> types = assemblyTypes.Where(m => !m.IsAbstract && !m.IsInterface && typeof(IHorseInterceptor).IsAssignableFrom(m));
        foreach (Type type in types) _services.AddSingleton(type);
        return this;
    }

    #endregion

    #region Horse Events

    /// <summary>
    /// Adds a event handler with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientHorseEvent<TEventHandler>() where TEventHandler : class, IHorseEventHandler
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient event handlers " +
                                            "Build HorseClient with IServiceCollection");

        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(_client.Event);
        registrar.RegisterHandler(typeof(TEventHandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));
        _services.AddTransient<TEventHandler>();
        return this;
    }

    /// <summary>
    /// Adds a event handler with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedHorseEvent<TEventHandler>() where TEventHandler : class, IHorseEventHandler
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped event handlers " +
                                            "Build HorseClient with IServiceCollection");

        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(_client.Event);
        registrar.RegisterHandler(typeof(TEventHandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));
        _services.AddScoped<TEventHandler>();
        return this;
    }

    /// <summary>
    /// Adds a event handler with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonHorseEvent<TEventHandler>() where TEventHandler : class, IHorseEventHandler
    {
        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(_client.Event);
        if (_services == null)
            registrar.RegisterHandler(typeof(TEventHandler));
        else
        {
            registrar.RegisterHandler(typeof(TEventHandler), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));
            _services.AddSingleton<TEventHandler>();
        }

        return this;
    }

    /// <summary>
    /// Adds all event handler types in specified assemblies with transient life time
    /// </summary>
    public HorseClientBuilder AddTransientHorseEvents(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use transient event handlers " +
                                            "Build HorseClient with IServiceCollection");

        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(_client.Event);
        IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient), assemblyTypes);
        foreach (Type type in types) _services.AddTransient(type);
        return this;
    }

    /// <summary>
    /// Adds all event handler types in specified assemblies with scoped life time
    /// </summary>
    public HorseClientBuilder AddScopedHorseEvents(params Type[] assemblyTypes)
    {
        if (_services == null)
            throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
                                            "If you want to use scoped event handlers " +
                                            "Build HorseClient with IServiceCollection");

        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(_client.Event);
        IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped), assemblyTypes);
        foreach (Type type in types) _services.AddScoped(type);
        return this;
    }

    /// <summary>
    /// Adds all event handler types in specified assemblies with singleton life time
    /// </summary>
    public HorseClientBuilder AddSingletonHorseEvents(params Type[] assemblyTypes)
    {
        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(_client.Event);
        if (_services == null)
            registrar.RegisterAssemblyHandlers(assemblyTypes);
        else
        {
            IEnumerable<Type> types = registrar.RegisterAssemblyHandlers(() => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton), assemblyTypes);
            foreach (Type type in types) _services.AddSingleton(type);
        }

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
    /// Action for message received events
    /// </summary>
    public HorseClientBuilder OnMessageReceived(Action<HorseMessage> action)
    {
        _client.MessageReceivedAction = action;
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

    #region Configurations

    /// <summary>
    /// Uses queue name handler
    /// </summary>
    public HorseClientBuilder UseQueueName(QueueNameHandler handler)
    {
        _client.Queue.NameHandler = handler;
        return this;
    }

    /// <summary>
    /// Uses channel name handler
    /// </summary>
    public HorseClientBuilder UseChannelName(ChannelNameHandler handler)
    {
        _client.Channel.NameHandler = handler;
        return this;
    }

    /// <summary>
    /// When application exit triggered, unsubscribes from queues and waits for active consume operations.
    /// Exit process is blocked minimum minWait and maximum maxWait.
    /// IF YOU ARE USING Microsoft.Extensions.Hosting, Hosting library can override and cancel that method's operations.
    /// Use UseGracefulShutdownHostedService method in Horse.Messaging.Extensions.Client library instead of that method.
    /// </summary>
    public HorseClientBuilder FinalizeConsumingOperations(TimeSpan minWait, TimeSpan maxWait)
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, args) => Shutdown(_client, minWait, maxWait);
        Console.CancelKeyPress += (sender, args) => { Shutdown(_client, minWait, maxWait); };

        return this;
    }

    private static void Shutdown(HorseClient client, TimeSpan minWait, TimeSpan maxWait)
    {
        using var shutdownCts = new CancellationTokenSource(maxWait);
        _ = client.Queue.UnsubscribeFromAllQueues(shutdownCts.Token);
        _ = client.Channel.UnsubscribeFromAllChannels(shutdownCts.Token);

        int min = Convert.ToInt32(minWait.TotalMilliseconds);
        int max = Convert.ToInt32(maxWait.TotalMilliseconds);

        Thread.Sleep(min);

        while (min < max)
        {
            if (client.Queue.ActiveConsumeOperations == 0)
                return;

            Thread.Sleep(250);
            min += 250;
        }
    }

    #endregion
}