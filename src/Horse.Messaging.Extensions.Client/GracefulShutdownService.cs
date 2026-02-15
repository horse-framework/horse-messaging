using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

internal class GracefulShutdownService(IServiceProvider provider, TimeSpan minWait, TimeSpan maxWait, Func<IServiceProvider, Task> shuttingDownAction)
    : IHostedService
{
    private readonly TimeSpan _maxWait = maxWait;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        HorseClient client = provider.GetService<HorseClient>();

        _ = client.Queue.UnsubscribeFromAllQueues();
        _ = client.Channel.UnsubscribeFromAllChannels();
        int min = Convert.ToInt32(minWait.TotalMilliseconds);
        int max = Convert.ToInt32(minWait.TotalMilliseconds);

        try
        {
            await shuttingDownAction?.Invoke(provider)!;
        }
        catch
        {
            // ignored
        }

        await Task.Delay(min, cancellationToken);

        while (min < max)
        {
            if (client.Queue.ActiveConsumeOperations == 0 && client.Channel.ActiveChannelOperations == 0)
                return;
            
            await Task.Delay(250, cancellationToken);
            min += 250;
        }
    }
}