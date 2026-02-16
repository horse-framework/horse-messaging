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
        _server.UseRider(_rider);
        await _server.RunAsync(2626, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _server.StopAsync(cancellationToken);
    }
}
