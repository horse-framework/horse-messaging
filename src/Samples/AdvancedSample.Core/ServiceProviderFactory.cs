using System;
using System.Runtime.CompilerServices;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("AdvancedSample.Service")]

namespace AdvancedSample.Core
{
	internal abstract class ServiceProviderFactory<T> : IServiceProviderFactory<IServiceCollection>
		where T : class
	{
		private readonly string _clientType;
		private readonly IConfiguration _configuration;

		private ILogger<HorseClient> _logger;
		private string _hostname;

		protected ServiceProviderFactory(string clientType, IConfiguration configuration)
		{
			_clientType = string.IsNullOrWhiteSpace(clientType) ? throw new ArgumentException("Client type must be defined", nameof(clientType)) : clientType;
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		}

		protected virtual void ConfigureHorseClient(HorseClientBuilder builder)
		{
			builder.SetHost(_hostname)
				   .SetClientType(_clientType)
				   .AddScopedConsumers(typeof(T))
				   .OnConnected(OnConnected)
				   .OnDisconnected(OnDisctonnected)
				   .OnMessageReceived(OnMessageReceived)
				   .OnError(OnError);
		}
		
		private void OnConnected(HorseClient client)
		{
			_logger.LogInformation("[CONNECTED] {Hosname}", _hostname);
		}

		private void OnDisctonnected(HorseClient client)
		{
			_logger.LogInformation("[DISCONNECTED] {Hosname}", _hostname);
		}

		private void OnMessageReceived(HorseMessage message)
		{
			_logger.LogInformation("[MESSAGE RECEVIED] {Message}", message.ToString());
		}

		private void OnError(Exception exception)
		{
			_logger.LogCritical(exception, "[ERROR]");
		}

		public virtual IServiceCollection CreateBuilder(IServiceCollection services)
		{
			HorseSettings horseSettings = _configuration.GetSection(nameof(HorseSettings)).Get<HorseSettings>();
			_hostname = horseSettings.ToString();
			services.AddHorseBus(ConfigureHorseClient);
			return services;
		}

		public IServiceProvider CreateServiceProvider(IServiceCollection services)
		{
			IServiceProvider provider = services.BuildServiceProvider();
			_logger = provider.GetRequiredService<ILogger<HorseClient>>();
			return provider.UseHorseBus();
		}
	}
}