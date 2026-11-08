using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Server.Queues;
using Xunit;

namespace Test.Queues.Core;

public class QueueRemovalIdentityTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Remove_StaleInstance_PreservesReplacementAndConfiguration(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        HorseQueue original = await ctx.Rider.Queue.Create("remove-identity");
        await ctx.Rider.Queue.Remove(original);

        HorseQueue replacement = await ctx.Rider.Queue.Create(original.Name);
        Assert.Equal(PushResult.Success, await replacement.Push("retained"));

        await ctx.Rider.Queue.Remove(original);

        Assert.Same(replacement, ctx.Rider.Queue.Find(original.Name));
        Assert.False(replacement.IsDestroyed);
        Assert.Equal(1, replacement.Manager.MessageStore.Count());
        Assert.NotNull(ctx.Rider.Queue.OptionsConfigurator.Find(x => x.Name == replacement.Name));
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Remove_ConcurrentCalls_DestroyRegisteredInstanceOnce(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        HorseQueue queue = await ctx.Rider.Queue.Create("remove-once");
        int destroyed = 0;
        queue.OnDestroyed += _ => Interlocked.Increment(ref destroyed);

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => ctx.Rider.Queue.Remove(queue))));

        Assert.Equal(1, destroyed);
        Assert.True(queue.IsDestroyed);
        Assert.Null(ctx.Rider.Queue.Find(queue.Name));
        Assert.Null(ctx.Rider.Queue.OptionsConfigurator.Find(x => x.Name == queue.Name));
    }
}
