using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Client.Events.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Cache;
using Xunit;

namespace Test.Cache;

public class CacheExpiredReadEventTest
{
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<HorseEvent>> Pending = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnDemandExpiration_PreservesRemoveNotification(bool increment)
    {
        await using var context = await CacheTestServer.Create();
        var client = new HorseClient();
        new EventSubscriberRegistrar(client.Event).RegisterHandler<RemoveNotificationHandler>();
        string key = "expired-event-" + Guid.NewGuid().ToString("N");
        var notification = new TaskCompletionSource<HorseEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(Pending.TryAdd(key, notification));
        try
        {
            await client.ConnectAsync($"horse://localhost:{context.Port}");
            var subscribed = await client.Event.Subscribe(HorseEventType.CacheRemove, null, true, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HorseResultCode.Ok, subscribed.Code);

            HorseCache cache = context.Rider.Cache;
            HorseCacheItem item = cache.Set(key, new MemoryStream(BitConverter.GetBytes(7)), TimeSpan.FromMinutes(5)).Item;
            object partition = typeof(HorseCache).GetMethod("GetPartition", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cache, [key]);
            object gate = partition.GetType().GetField("Gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(partition);
            // Expire and read while holding the key gate, so the background
            // cleaner cannot turn this into a timer-expiration test.
            lock (gate)
            {
                item.Expiration = DateTime.UtcNow.AddSeconds(-1);
                if (increment)
                {
                    var result = cache.GetIncremental(key, TimeSpan.FromMinutes(5));
                    Assert.Equal(1, BitConverter.ToInt32(result.Item.Value.ToArray()));
                }
                else
                    Assert.False(cache.TryGet(key, out _));
            }

            HorseEvent received = await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HorseEventType.CacheRemove, received.Type);
            Assert.Equal(key, received.Target);
        }
        finally
        {
            Pending.TryRemove(key, out _);
            client.Disconnect();
        }
    }

    [HorseEventAttribute(HorseEventType.CacheRemove)]
    public sealed class RemoveNotificationHandler : IHorseEventHandler
    {
        public Task Handle(HorseEvent horseEvent, HorseClient client)
        {
            if (horseEvent.Target != null && Pending.TryGetValue(horseEvent.Target, out var completion))
                completion.TrySetResult(horseEvent);
            return Task.CompletedTask;
        }
    }
}
