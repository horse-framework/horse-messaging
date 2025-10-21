using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Server;
using HostedServiceSample.Server.Handlers;
using HostedServiceSample.Server.RouteBindings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HostOptions = Horse.Server.HostOptions;

namespace HostedServiceSample.Server;

internal class HostedService : IHostedService
{
    private readonly HorseServer _server;
    private readonly ILogger<HostedService> _logger;
    private readonly HorseRiderBuilder _riderBuilder;
    private readonly IQueueEventHandler _queueEventHandler;
    private readonly IClientHandler _clientHandler;
    private readonly IErrorHandler _errorHandler;

    public HostedService(IOptions<ServerOptions> options,
        ILogger<HostedService> logger,
        IQueueEventHandler queueEventHandler,
        IClientHandler clientHandler,
        IErrorHandler errorHandler)
    {
        _logger = logger;
        _queueEventHandler = queueEventHandler;
        _clientHandler = clientHandler;
        _errorHandler = errorHandler;
        _server = new HorseServer(options.Value);
        _riderBuilder = CreateHorseRiderBuilder();
        _server.OnStarted += Started;
        _server.OnStopped += Stopped;
        _server.OnInnerException += ExceptionThrown;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        HorseRider rider = _riderBuilder.Build();
        rider.ConfigureServiceRoutes();
        _server.UseRider(rider);
        _server.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server.Stop();
        return Task.CompletedTask;
    }

    private HorseRiderBuilder CreateHorseRiderBuilder()
    {
        return HorseRiderBuilder.Create()
            .ConfigureQueues(cfg =>
            {
                cfg.EventHandlers.Add(_queueEventHandler);
                DataConfigurationBuilder builder = new();
                builder.KeepLastBackup();
                builder.UseAutoFlush(TimeSpan.FromMilliseconds(500));
                cfg.UseCustomQueueManager("a", m =>
                {
                    var manager = new SamplePersistentQueueManager(m.Queue, builder.CreateOptions(m.Queue), true);
                    return Task.FromResult<IHorseQueueManager>(manager);
                });
                cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(15);
                cfg.Options.Type = QueueType.RoundRobin;
                cfg.Options.AutoQueueCreation = true;
            })
            .ConfigureClients(cfg => { cfg.Handlers.Add(_clientHandler); })
            .ConfigureOptions(options => { options.Name = "SAMPLE"; })
            .ConfigureRouters(o => o.Rider.Router.KeepRouters = false)
            .AddErrorHandler(_errorHandler);
    }

    private void Started(HorseServer obj)
    {
        _logger.LogInformation("[HORSE SERVER] Started");
        foreach (HostOptions host in _server.Options.Hosts)
        foreach (string hostname in host.Hostnames)
            _logger.LogInformation("{Protocol}://{Hostname}:{Port}", host.SslEnabled ? "hmqs" : "hmq", hostname, host.Port);
    }

    private void Stopped(HorseServer obj)
    {
        _logger.LogInformation("[HORSE SERVER] Stopped");
    }

    private void ExceptionThrown(HorseServer server, Exception ex)
    {
        _logger.LogCritical(ex, "[ERROR] {Message}", ex.Message);
    }
}