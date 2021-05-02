using System;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedSample.Core.Service
{
	internal class ServiceProviderFactory<T> : IServiceProviderFactory<IServiceCollection>, IRegistrar
		where T : notnull, IServiceStartup
	{
		private readonly string _clientType;
		private readonly string _host;
		private IServiceProvider _provider;
		private HorseClientBuilder _clientBuilder;

		public ServiceProviderFactory(string clientType)
		{
			_clientType = clientType;
			_host = "horse://localhost:15500";
		}

		public IServiceCollection CreateBuilder(IServiceCollection services)
		{
			services.AddHorseBus(BuildHorseClient);
			return services;
		}

		public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
		{
			_provider = containerBuilder.BuildServiceProvider();
			_provider.UseHorseBus();
			return _provider;
		}

		private void BuildHorseClient(HorseClientBuilder builder)
		{
			_clientBuilder = builder.SetHost(_host)
									.SetClientType(_clientType)
									.SetReconnectWait(TimeSpan.FromSeconds(1))
									.OnConnected(OnConnected)
									.OnDisconnected(OnDisconnected)
									.OnMessageReceived(OnMessageReceived)
									.OnError(OnError);
		}

		private static void OnError(Exception exception)
		{
			_ = Console.Out.WriteLineAsync(exception.Message);
		}

		private static void OnMessageReceived(HorseMessage message)
		{
			_ = Console.Out.WriteLineAsync(message.ToString());
		}

		private static void OnDisconnected(HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("DISCONNECTED!");
		}

		private static void OnConnected(HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("CONNECTED!");
		}

		#region REGISTRAR

		public void AddTransientConsumers() => _clientBuilder.AddTransientConsumers(typeof(T));
		public void AddTransientSubscribers() => _clientBuilder.AddTransientChannelSubscribers(typeof(T));
		public void AddTransientDirectHandlers() => _clientBuilder.AddTransientDirectHandlers(typeof(T));
		public void AddTransientHorseEvents() => _clientBuilder.AddTransientHorseEvents(typeof(T));
		public void AddScopedConsumers() => _clientBuilder.AddScopedConsumers(typeof(T));
		public void AddScopedSubscribers() => _clientBuilder.AddScopedChannelSubscribers(typeof(T));
		public void AddScopedDirectHandlers() => _clientBuilder.AddScopedDirectHandlers(typeof(T));
		public void AddScopedHorseEvents() => _clientBuilder.AddScopedHorseEvents(typeof(T));
		public void AddSingletonConsumers() => _clientBuilder.AddSingletonConsumers(typeof(T));
		public void AddSingletonSubscribers() => _clientBuilder.AddSingletonChannelSubscribers(typeof(T));
		public void AddSingletonDirectHandlers() => _clientBuilder.AddSingletonDirectHandlers(typeof(T));
		public void AddSingletonHorseEvents() => _clientBuilder.AddSingletonHorseEvents(typeof(T));

		public void AddTransientConsumer<TConsumer>() where TConsumer : class => _clientBuilder.AddTransientConsumer<TConsumer>();
		public void AddTransientChannelSubscriber<TChannelSubscriber>() where TChannelSubscriber : class => _clientBuilder.AddTransientChannelSubscriber<TChannelSubscriber>();
		public void AddTransientDirectHandler<TDirectHandler>() where TDirectHandler : class => _clientBuilder.AddTransientDirectHandler<TDirectHandler>();
		public void AddTransientHorseEvent<THorseEvent>() where THorseEvent : IHorseEventHandler => _clientBuilder.AddTransientHorseEvent<THorseEvent>();
		public void AddScopedConsumer<TConsumer>() where TConsumer : class => _clientBuilder.AddScopedConsumer<TConsumer>();
		public void AddScopedChannelSubscriber<TChannelSubscriber>() where TChannelSubscriber : class => _clientBuilder.AddScopedChannelSubscriber<TChannelSubscriber>();
		public void AddScopedDirectHandler<TDirectHandler>() where TDirectHandler : class => _clientBuilder.AddScopedDirectHandler<TDirectHandler>();
		public void AddScopedHorseEvent<THorseEvent>() where THorseEvent : IHorseEventHandler => _clientBuilder.AddScopedHorseEvent<THorseEvent>();
		public void AddSingletonConsumer<TConsumer>() where TConsumer : class => _clientBuilder.AddSingletonConsumer<TConsumer>();
		public void AddSingletonChannelSubscriber<TChannelSubscriber>() where TChannelSubscriber : class => _clientBuilder.AddSingletonChannelSubscriber<TChannelSubscriber>();
		public void AddSingletonDirectHandler<TDirectHandler>() where TDirectHandler : class => _clientBuilder.AddSingletonDirectHandler<TDirectHandler>();
		public void AddSingletonHorseEvent<THorseEvent>() where THorseEvent : IHorseEventHandler => _clientBuilder.AddSingletonHorseEvent<THorseEvent>();

		#endregion
	}
}