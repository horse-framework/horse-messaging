using System;
using System.Threading.Tasks;
using AdvancedSample.Core;
using Horse.Messaging.Client;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PullQueueSample.Client
{
	internal abstract class HorseServiceBase: IHorseService
	{
		private string _hostname;
		private IHost _host;
		private ILogger<HorseServiceBase> _logger;

		private readonly string[] _args;
		private readonly string _clientType;
		private readonly int _debugUserId;

		private Action<HorseClientBuilder> _clientBuilderDelegate;
		private Action<IServiceCollection> _configureDelegate1;
		private Action<IServiceCollection, IConfiguration> _configureDelegate2;
		private Action<IHostBuilder> _hostBuilderDelegate;

		protected HorseServiceBase(string[] args, string clientType)
		{
			_args = args;
			_clientType = clientType;
			_debugUserId = args.Length > 0 ? int.Parse(args[0]) : 0;
		}


		public void Run()
		{
			Build();
			using (_host)
			{
				_host.Start();
				_host.Services.UseHorseBus();
				_host.WaitForShutdown();
			}
		}

		public Task RunAsync()
		{
			Build();
			using (_host)
			{
				_host.Start();
				_host.Services.UseHorseBus();
				return _host.WaitForShutdownAsync();
			}
		}

		public HorseClient HorseClient { get; private set; }

		public void ConfigureHorseClient(Action<HorseClientBuilder> builderDelegate)
		{
			_clientBuilderDelegate = builderDelegate;
		}

		public void ConfigureHostBuilder(Action<IHostBuilder> hostBuilderDelegate)
		{
			_hostBuilderDelegate = hostBuilderDelegate;
		}

		public void ConfigureServices(Action<IServiceCollection> configureDelegate)
		{
			_configureDelegate1 = configureDelegate;
		}

		public void ConfigureServices(Action<IServiceCollection, IConfiguration> configureDelegate)
		{
			_configureDelegate2 = configureDelegate;
		}

		private void Build()
		{
			_host = BuildHost(_args);
		}

		private IHost BuildHost(string[] args)
		{
			IHostBuilder builder = Host.CreateDefaultBuilder(args)
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
			IHost host = builder.Build();
			_logger = host.Services.GetRequiredService<ILogger<HorseServiceBase>>();
			HorseClient = host.Services.GetRequiredService<HorseClient>();
			return host;
		}

		private void ConfigureHorseClient(HostBuilderContext hostContext, HorseClientBuilder builder)
		{
			HorseOptions options = hostContext.Configuration.GetSection(nameof(HorseOptions)).Get<HorseOptions>();
			_hostname = options.ToString();
			_clientBuilderDelegate?.Invoke(builder);
			builder.AddHost(_hostname)
				   .SetClientType(_clientType)
				   .SetClientName(_debugUserId.ToString())
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