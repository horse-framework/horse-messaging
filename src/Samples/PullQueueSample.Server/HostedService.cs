using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PullQueueSample.Server;

internal class HostedService: IHostedService
{
    private readonly HorseRiderBuilder _riderBuilder;
    private readonly HorseServer _server;

    public HostedService(IOptions<ServerOptions> options)
    {
        _riderBuilder = CreateHorseRiderBuilder();
        _server = new HorseServer(options.Value);
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HorseRider rider = _riderBuilder.Build();
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
                cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(15);
                cfg.Options.AutoQueueCreation = true;
                cfg.Options.Type = QueueType.Pull;
            })
            .ConfigureOptions(options => { options.Name = "SAMPLE"; });
    }
}