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
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server.UseRider(_rider);
        _server.Start(2626);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server.Stop();
        return Task.CompletedTask;
    }
}
