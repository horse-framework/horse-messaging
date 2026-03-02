using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Horse.Server;
using Microsoft.Extensions.Hosting;

namespace Sample.All;

internal class ServerHostedService : IHostedService
{
    private readonly HorseServer _server = new();
    private readonly HorseRider _rider = HorseRiderBuilder.Create()
        .ConfigureQueues((cfg) =>
        {
            cfg.UsePersistentQueues(persistentQueues =>
            {
                persistentQueues.SetAutoShrink(true, TimeSpan.FromMinutes(10));
                persistentQueues.UseInstantFlush();
            });
            cfg.Options.Type = QueueType.RoundRobin;
            cfg.Options.PutBackDelay = 1000;
            cfg.Options.PutBack = PutBackDecision.Regular;
            cfg.Options.Acknowledge = QueueAckDecision.JustRequest;
            cfg.Options.AcknowledgeTimeout = TimeSpan.FromMinutes(30);
            cfg.Options.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = TimeSpan.FromHours(1).Seconds,
                Policy = MessageTimeoutPolicy.NoTimeout
            };
            cfg.Options.Partition = new PartitionOptions
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
        _server.UseRider(_rider);
        await _server.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _server.StopAsync(cancellationToken);
    }
}
