using AdvancedSample.Core;
using AdvancedSample.Messaging.Server.Handlers;
using Horse.Messaging.Server;
using Horse.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedSample.Messaging.Server
{
	public class AdvancedSampleServer
	{
		private readonly IHostBuilder _hostBuilder;
		private IHost _host;

		public AdvancedSampleServer(string[] args)
		{
			_hostBuilder = CreateHostBuilder(args);
		}

		private static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
					   .ConfigureHostConfiguration(builder => builder.ConfigureHost())
					   .ConfigureAppConfiguration((hostContext, builder) => builder.ConfigureApp(hostContext))
					   .ConfigureServices((hostContext, services) =>
										  {
											  services.AddHostedService<AdvancedSampleHostedService>();
											  services.AddSingleton<IQueueEventHandler, AdvancedSampleQueueEventHandler>();
											  services.AddSingleton<IClientHandler, AdvancedSampleClientHandler>();
											  services.AddSingleton<IErrorHandler, AdvancedSampleErrorHandler>();
											  services.Configure<ServerOptions>(options => hostContext.Configuration.GetSection("HorseServerOptions").Bind(options));
										  });
		}

		public void Run()
		{
			_host ??= _hostBuilder.Build();
			_host.Run();
		}
	}
}