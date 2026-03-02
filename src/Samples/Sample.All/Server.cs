using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues.Partitions;
using Horse.Server;
using Microsoft.Extensions.Hosting;

namespace Sample.All;

internal class ServerHostedService : IHostedService
{
    private readonly HorseServer _server = new();
    internal readonly HorseRider Rider = HorseRiderBuilder.Create()
        .ConfigureQueues((config) =>
        {
            config.UseMemoryQueues();
            config.Options.Partition = new PartitionOptions
            {
                Enabled = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 50,
                AutoDestroyIdleSeconds = TimeSpan.FromMinutes(10).Seconds
            };
        })
        .Build();
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _server.Options.Hosts = [new HorseHostOptions { Port = 2626 }];
        _server.UseRider(Rider);
        await _server.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _server.StopAsync(cancellationToken);
    }
}
