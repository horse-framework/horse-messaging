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
	public abstract class SampleServiceBase
	{
		private string _hostname;
		private IHost _host;
		private ILogger<SampleServiceBase> _logger;

		private readonly string _clientType;
		private readonly string[] _args;

		private Action<HorseClientBuilder> _clientBuilderDelegate;
		private Action<IServiceCollection> _configureDelegate1;
		private Action<IServiceCollection, IConfiguration> _configureDelegate2;
		private Action<IHostBuilder> _hostBuilderDelegate;

		protected SampleServiceBase(string clientType, string[] args)
		{
			_args = args;
			_clientType = string.IsNullOrWhiteSpace(clientType) ? throw new ArgumentException("Client type must be defined", nameof(clientType)) : clientType;
		}

		public void Build()
		{
			_host = BuildHost(_args);
		}

		public void Run()
		{
			Build();
			using (_host)
			{
				_host.StartAsync();
				_host.Services.UseHorseBus();
				_host.WaitForShutdown();
			}
		}

		protected void ConfigureHorseClient(Action<HorseClientBuilder> builderDelegate)
		{
			_clientBuilderDelegate = builderDelegate;
		}

		protected void ConfigureHostBuilder(Action<IHostBuilder> hostBuilderDelegate)
		{
			_hostBuilderDelegate = hostBuilderDelegate;
		}

		protected void ConfigureServices(Action<IServiceCollection> configureDelegate)
		{
			_configureDelegate1 = configureDelegate;
		}

		protected void ConfigureServices(Action<IServiceCollection, IConfiguration> configureDelegate)
		{
			_configureDelegate2 = configureDelegate;
		}

		private IHost BuildHost(string[] args)
		{
			var builder = Host.CreateDefaultBuilder(args)
							  .ConfigureHostConfiguration(builder => builder.ConfigureHost())
							  .ConfigureAppConfiguration((hostContext, builder) => builder.ConfigureApp(hostContext))
							  .UseServiceProviderFactory(hostContext => new HorseServiceProviderFactory(hostContext, false))
							  .ConfigureHorseClient(ConfigureHorseClient)
							  .ConfigureServices((hostContext, services) =>
												 {
													 _configureDelegate1?.Invoke(services);
													 _configureDelegate2?.Invoke(services, hostContext.Configuration);
												 });
			_hostBuilderDelegate?.Invoke(builder);
			var host = builder.Build();
			_logger = host.Services.GetRequiredService<ILogger<SampleServiceBase>>();
			return host;
		}

		private void ConfigureHorseClient(HostBuilderContext hostContext, HorseClientBuilder builder)
		{
			HorseOptions options = hostContext.Configuration.GetSection(nameof(HorseOptions)).Get<HorseOptions>();
			_hostname = options.ToString();
			_clientBuilderDelegate?.Invoke(builder);
			builder.SetHost(_hostname)
				   .SetClientType(_clientType)
				   .SetResponseTimeout(TimeSpan.FromSeconds(15))
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
			string requestId = message.FindHeader("RequestId");
			string userId = message.FindHeader("UserId");
			_logger.LogInformation("[MESSAGE RECEVIED] [{RequesId}][{UserId}] <{ContentType}> | {Message}", requestId, userId, message.ContentType, message.ToString());
		}

		private void OnError(Exception exception)
		{
			_logger.LogCritical(exception, "[ERROR]");
		}
	}
}