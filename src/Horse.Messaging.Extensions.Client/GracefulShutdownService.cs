using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

internal class GracefulShutdownService(
    IServiceProvider provider,
    string serviceKey,
    TimeSpan minWait,
    TimeSpan maxWait,
    Func<Task> shuttingDownAction,
    Func<IServiceProvider, Task> shuttingDownActionWithProvider)
    : IHostedService
{
    private volatile bool _executed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executed) return;
        _executed = true;

        HorseClient client = string.IsNullOrWhiteSpace(serviceKey) 
            ? provider.GetRequiredService<HorseClient>() 
            : provider.GetRequiredKeyedService<HorseClient>(serviceKey);

        // Mark client as shutdown executed to prevent HorseClient's internal handlers from running
        client.GracefulShutdownExecuted = true;

        await client.Queue.UnsubscribeFromAllQueues();
        await client.Channel.UnsubscribeFromAllChannels();
        
        int min = Convert.ToInt32(minWait.TotalMilliseconds);
        int max = Convert.ToInt32(maxWait.TotalMilliseconds);

        try
        {
            if (shuttingDownAction != null)
                await shuttingDownAction.Invoke();

            if (shuttingDownActionWithProvider != null)
                await shuttingDownActionWithProvider.Invoke(provider);
        }
        catch
        {
            // ignored
        }

        // Use CancellationToken.None to prevent TaskCanceledException during shutdown
        await Task.Delay(min, CancellationToken.None);

        while (min < max)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            if (client.Queue.ActiveConsumeOperations == 0 && client.Channel.ActiveChannelOperations == 0)
                break;
            
            await Task.Delay(250, CancellationToken.None);
            min += 250;
        }
        
        client.Disconnect();
    }
}