using System;
using Horse.Messaging.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedSample.Core.Service
{
	internal class ServiceProviderFactory<T> : IServiceProviderFactory<IServiceCollection>
	{
		private readonly string _clientType;
		private readonly string _host;
		private IServiceProvider _provider;

		public ServiceProviderFactory(string clientType)
		{
			_clientType = clientType;
			_host = "horse://localhost:15500";
		}

		public IServiceCollection CreateBuilder(IServiceCollection services)
		{
			services.AddControllers()
					.SetCompatibilityVersion(CompatibilityVersion.Latest)
					.PartManager.ApplicationParts.Add(new AssemblyPart(typeof(T).Assembly));

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
			builder.SetHost(_host)
				   .SetClientType(_clientType)
				   .SetReconnectWait(TimeSpan.FromSeconds(1))
				   .UseNewtonsoftJsonSerializer()
				   .OnConnected(OnConnected)
				   .OnDisconnected(OnDisconnected)
				   .OnError(OnError);
		}

		private static void OnError(Exception exception)
		{
			_ = Console.Out.WriteLineAsync(exception.Message);
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