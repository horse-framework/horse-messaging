using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Hosted service that connects the <see cref="HorseClient"/> when the host starts.
/// Registered automatically when <c>autoConnect = true</c> (the default) in AddHorse overloads.
/// </summary>
internal sealed class HorseConnectService(IServiceProvider provider, string serviceKey) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HorseClient client = string.IsNullOrWhiteSpace(serviceKey)
            ? provider.GetRequiredService<HorseClient>()
            : provider.GetRequiredKeyedService<HorseClient>(serviceKey);

        client.Provider = provider;
        client.Connect();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

