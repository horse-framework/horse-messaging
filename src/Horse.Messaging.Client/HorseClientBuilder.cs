using System;
using System.Collections.Generic;
using System.Linq;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Client
{
	/// <inheritdoc />
	public class HorseClientBuilder<TIdentifier> : HorseClientBuilder
	{
		/// <summary>
		/// Creates Horse Connector Builder without IOC implementation
		/// </summary>
		public HorseClientBuilder() : base(new HorseClient<TIdentifier>()) { }

		/// <summary>
		/// Creates Horse Connector Builder without IOC implementation
		/// </summary>
		public HorseClientBuilder(IServiceCollection services) : base(services, new HorseClient<TIdentifier>()) { }

		/// <summary>
		/// Builds new HorseClient with defined properties.
		/// </summary>
		public override HorseClient<TIdentifier> Build()
		{
			return (HorseClient<TIdentifier>) base.Build();
		}
	}

	/// <summary>
	/// Horse Client Builder
	/// </summary>
	public class HorseClientBuilder
	{
		#region Declaration

		private readonly HorseClient _client;
		private IServiceCollection _services;

		/// <summary>
		/// Creates Horse Connector Builder without IOC implementation
		/// </summary>
		public HorseClientBuilder()
		{
			_client = new HorseClient();
		}

		internal HorseClientBuilder(HorseClient client)
		{
			_client = client;
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
		/// Creates Horse Connector Builder with IOC implementation
		/// </summary>
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
			return _client;
		}

		/// <summary>
		/// Adds MSDI implementation
		/// </summary>
		public HorseClientBuilder AddServices(IServiceCollection services)
		{
			_services = services;
			return this;
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
		public HorseClientBuilder AddTransientConsumer<TConsumer>() where TConsumer : class
		{
			if (_services == null)
				throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
												"If you want to use transient queue consumers " +
												"Build HorseClient with IServiceCollection");

			QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
			registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Transient));
			_services.AddTransient<TConsumer>();
			return this;
		}

		/// <summary>
		/// Adds a queue consumer with scoped life time
		/// </summary>
		public HorseClientBuilder AddScopedConsumer<TConsumer>() where TConsumer : class
		{
			if (_services == null)
				throw new NotSupportedException("Only Singleton lifetime is supported without MSDI Implementation. " +
												"If you want to use scoped queue consumers " +
												"Build HorseClient with IServiceCollection");

			QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
			registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Scoped));
			_services.AddScoped<TConsumer>();
			return this;
		}

		/// <summary>
		/// Adds a queue consumer with singleton life time
		/// </summary>
		public HorseClientBuilder AddSingletonConsumer<TConsumer>() where TConsumer : class
		{
			QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(_client.Queue);
			if (_services == null)
				registrar.RegisterConsumer(typeof(TConsumer));
			else
			{
				registrar.RegisterConsumer(typeof(TConsumer), () => new MicrosoftDependencyHandlerFactory(_client, ServiceLifetime.Singleton));
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
		/// Configure default type descriptors for queues
		/// </summary>
		public HorseClientBuilder ConfigureQueueTypes(Action<DefaultQueueTypeConfigurator> cfg)
		{
			DefaultQueueTypeConfigurator configurator = new DefaultQueueTypeConfigurator(_client.Queue);
			cfg(configurator);
			return this;
		}

		#endregion
	}
}