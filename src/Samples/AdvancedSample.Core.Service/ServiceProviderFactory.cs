using System;
using System.Collections.Generic;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedSample.Core.Service
{
	internal class ServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
	{
		private readonly string _clientType;
		private readonly string _host;
		private IServiceProvider _provider;
		private readonly List<Action<HorseClientBuilder>> _addHandlers = new();

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

		public void AddTransientHandlers<T>()
		{
			_addHandlers.Add((builder) => builder.AddTransientDirectHandlers(typeof(T)));
		}

		private void BuildHorseClient(HorseClientBuilder builder)
		{
			foreach (var handler in _addHandlers)
				handler(builder);

			builder.SetHost(_host)
				   .SetClientType(_clientType)
				   .SetReconnectWait(TimeSpan.FromSeconds(1))
				   .UseNewtonsoftJsonSerializer()
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
	}
}