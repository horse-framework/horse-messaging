using System;
using AdvancedSample.Core;
using Horse.Messaging.Client;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdvancedSample.Service
{
	public class SampleServiceBuilder { }

	public sealed class SampleService<T> where T : class
	{
		private readonly string _clientType;
		private readonly IHost _host;
		private string _hostname;
		private Action<HorseClientBuilder> _clientBuilderDelegate;
		private ILogger<SampleService<T>> _logger;

		public SampleService(string clientType, string[] args)
		{
			_clientType = string.IsNullOrWhiteSpace(clientType) ? throw new ArgumentException("Client type must be defined", nameof(clientType)) : clientType;
			_host = BuildHost(args);
		}

		public void Run()
		{
			using (_host)
			{
				_host.StartAsync();
				_host.Services.UseHorseBus();
				_host.WaitForShutdown();
			}
		}

		public void ConfigureHorseClient(Action<HorseClientBuilder> builderDelegate)
		{
			_clientBuilderDelegate = builderDelegate;
		}

		private IHost BuildHost(string[] args)
		{
			IHost host = Host.CreateDefaultBuilder(args)
							 .ConfigureHostConfiguration(builder => builder.ConfigureHost())
							 .ConfigureAppConfiguration((hostContext, builder) => builder.ConfigureApp(hostContext))
							 .UseServiceProviderFactory(hostContext => new HorseServiceProviderFactory(hostContext, false))
							 .ConfigureHorseClient(ConfigureHorseClient)
							 .Build();
			_logger = host.Services.GetRequiredService<ILogger<SampleService<T>>>();
			return host;
		}

		private void ConfigureHorseClient(HostBuilderContext hostContext, HorseClientBuilder builder)
		{
			HorseSettings horseSettings = hostContext.Configuration.GetSection(nameof(HorseSettings)).Get<HorseSettings>();
			_hostname = horseSettings.ToString();
			_clientBuilderDelegate?.Invoke(builder);
			builder.SetHost(_hostname)
				   .SetClientType(_clientType)
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
	}
}