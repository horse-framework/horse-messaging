using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Hosted service that performs a graceful shutdown of the <see cref="HorseClient"/>
/// when the host stops. Delegates to the client's internal graceful shutdown logic,
/// passing the host's cancellation token so the host shutdown timeout is respected.
/// </summary>
internal sealed class GracefulShutdownService(IServiceProvider provider, string serviceKey) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        HorseClient client = string.IsNullOrWhiteSpace(serviceKey)
            ? provider.GetRequiredService<HorseClient>()
            : provider.GetRequiredKeyedService<HorseClient>(serviceKey);

        await client.GracefulShutdownAsync(cancellationToken);
    }
}