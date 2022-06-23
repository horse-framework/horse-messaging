using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

internal class GracefulShutdownService : IHostedService
{
    private readonly TimeSpan _minWait;
    private readonly TimeSpan _maxWait;
    private readonly IServiceProvider _provider;
    private readonly Func<Task> _shuttingDownAction;

    public GracefulShutdownService(IServiceProvider provider, TimeSpan minWait, TimeSpan maxWait, Func<Task> shuttingDownAction)
    {
        _provider = provider;
        _minWait = minWait;
        _maxWait = maxWait;
        _shuttingDownAction = shuttingDownAction;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        HorseClient client = _provider.GetService<HorseClient>();

        _ = client.Queue.UnsubscribeFromAllQueues();
        int min = Convert.ToInt32(_minWait.TotalMilliseconds);
        int max = Convert.ToInt32(_minWait.TotalMilliseconds);

        try
        {
            await _shuttingDownAction?.Invoke();
        }
        catch
        {
        }

        await Task.Delay(min, cancellationToken);

        while (min < max)
        {
            if (client.Queue.ActiveConsumeOperations == 0)
                return;

            await Task.Delay(250, cancellationToken);
            min += 250;
        }
    }
}