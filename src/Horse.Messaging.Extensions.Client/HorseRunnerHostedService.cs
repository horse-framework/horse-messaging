using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

internal class HorseRunnerHostedService : IHostedService
{
    private readonly IServiceProvider _provider;

    public HorseRunnerHostedService(IServiceProvider provider)
    {
        _provider = provider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _provider.UseHorseBus();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}