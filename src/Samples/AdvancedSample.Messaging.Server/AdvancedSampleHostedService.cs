using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedSample.Messaging.Server.RouteBindings;
using Horse.Messaging.Data;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HostOptions = Horse.Server.HostOptions;

namespace AdvancedSample.Messaging.Server
{
    public class AdvancedSampleHostedService : IHostedService
    {
        private readonly HorseServer _server;
        private readonly ILogger<AdvancedSampleHostedService> _logger;
        private readonly HorseRiderBuilder _riderBuilder;
        private readonly IQueueEventHandler _queueEventHandler;
        private readonly IClientHandler _clientHandler;
        private readonly IErrorHandler _errorHandler;

        public AdvancedSampleHostedService(IOptions<ServerOptions> options,
                                           ILogger<AdvancedSampleHostedService> logger,
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
                    cfg.UsePersistentQueues(q =>
                    {
                        q.UseAutoFlush(TimeSpan.FromMilliseconds(500));
                        q.KeepLastBackup();
                    }, DeleteWhen.AfterAcknowledge, CommitWhen.AfterAcknowledge, true);

                    cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
                    cfg.Options.Type = QueueType.RoundRobin;
                    cfg.Options.AutoQueueCreation = true;
                })
                .ConfigureClients(cfg => { cfg.Handlers.Add(_clientHandler); })
                .ConfigureOptions(options => { options.Name = "SAMPLE"; })
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
}