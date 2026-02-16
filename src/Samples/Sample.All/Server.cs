using Horse.Messaging.Server;
using Horse.Server;
using Microsoft.Extensions.Hosting;

namespace Sample.All;

internal class ServerHostedService : IHostedService
{
    private readonly HorseServer _server = new();
    private readonly HorseRider _rider = HorseRiderBuilder.Create()
        .ConfigureQueues((config) =>
        {
            config.UseMemoryQueues();
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
