using Horse.Messaging.Server;
using Horse.Server;
using HostedServiceSample.Common;
using HostedServiceSample.Server.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HostedServiceSample.Server;

internal class Server
{
    private readonly IHostBuilder _hostBuilder;
    private IHost _host;

    public Server(string[] args)
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
                services.AddHostedService<HostedService>();
                services.AddSingleton<IQueueEventHandler, QueueEventHandler>();
                services.AddSingleton<IClientHandler, ClientHandler>();
                services.AddSingleton<IErrorHandler, ErrorHandler>();
                services.Configure<ServerOptions>(options => hostContext.Configuration.GetSection("HorseServerOptions").Bind(options));
            });
    }

    public void Run()
    {
        _host ??= _hostBuilder.Build();
        _host.Run();
    }
}