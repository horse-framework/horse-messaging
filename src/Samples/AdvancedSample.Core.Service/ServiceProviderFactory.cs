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
			_addHandlers.Add((builder) => builder.AddTransientConsumers(typeof(T)));
		}

		private void BuildHorseClient(HorseClientBuilder builder)
		{
			foreach (var handler in _addHandlers)
				handler(builder);

			builder.BuildHorseClient(_host, _clientType);
		}
	}
}