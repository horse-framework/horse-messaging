using System;
using System.Reflection;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Options;

public class MessageTimeoutForwardingTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushQueue_CreatesMissingTarget_AndForwardsBothPriorities(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.AutoQueueCreation = false;
            o.MessageTimeout = new MessageTimeoutStrategy();
        });

        const string targetName = "timeout-created-target";
        HorseQueue source = await CreateSource(ctx, "timeout-create-source", targetName);
        Assert.Null(ctx.Rider.Queue.Find(targetName));
        Assert.Equal(PushResult.Success, await source.Push(CreateMessage(source.Name, "normal")));
        HorseMessage priority = CreateMessage(source.Name, "priority");
        priority.HighPriority = true;
        Assert.Equal(PushResult.Success, await source.Push(priority));

        Assert.True(await WaitUntil(() =>
        {
            HorseQueue target = ctx.Rider.Queue.Find(targetName);
            return source.IsEmpty && target?.Manager != null &&
                   target.Manager.MessageStore.Count() == 1 &&
                   target.Manager.PriorityMessageStore.Count() == 1;
        }), "expired messages were not moved to the automatically created target");

        HorseQueue created = ctx.Rider.Queue.Find(targetName);
        Assert.Equal(QueueStatus.Running, created.Status);
        Assert.Equal("Default", created.ManagerName);
        Assert.NotNull(created.Manager.MessageStore.Find("normal"));
        Assert.NotNull(created.Manager.PriorityMessageStore.Find("priority"));
    }

    [Theory]
    [InlineData("memory", NodeState.Successor)]
    [InlineData("memory", NodeState.Replica)]
    [InlineData("persistent", NodeState.Successor)]
    [InlineData("persistent", NodeState.Replica)]
    public async Task PassiveNode_ExpiresLocally_WithoutCreatingForwardTarget(string mode, NodeState state)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        const string targetName = "passive-timeout-target";
        HorseQueue source = await CreateSource(ctx, "passive-timeout-source", targetName);
        ctx.Rider.Cluster.Options.Mode = ClusterMode.Reliable;
        typeof(ClusterManager).GetProperty(nameof(ClusterManager.State), BindingFlags.Instance | BindingFlags.Public)
            .SetValue(ctx.Rider.Cluster, state);

        Assert.Equal(PushResult.Success, await source.PushByNode(CreateMessage(source.Name, "replicated")));
        Assert.False(source.IsEmpty);
        Assert.True(await WaitUntil(() => source.IsEmpty), "passive node did not remove its expired message");
        Assert.Null(ctx.Rider.Queue.Find(targetName));
        Assert.Equal(state, ctx.Rider.Cluster.State);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushQueue_PreservesSource_WhenTargetRejectsMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        HorseQueue target = await ctx.Rider.Queue.Create("timeout-paused-target");
        target.SetStatus(QueueStatus.Paused);
        HorseQueue source = await CreateSource(ctx, "timeout-rejected-source", target.Name);
        TimeoutErrorHandler errors = new();
        ctx.Rider.ErrorHandlers.Add(errors);

        Assert.Equal(PushResult.Success, await source.Push(CreateMessage(source.Name, "rejected")));
        Task completed = await Task.WhenAny(errors.Failed.Task, Task.Delay(12000));
        Assert.Same(errors.Failed.Task, completed);
        Assert.NotNull(source.Manager.MessageStore.Find("rejected"));
        Assert.True(target.IsEmpty);
    }

    private static Task<HorseQueue> CreateSource(QueueTestContext ctx, string name, string targetName)
    {
        return ctx.Rider.Queue.Create(name, o =>
        {
            o.Type = QueueType.Pull;
            o.MessageTimeout = new MessageTimeoutStrategy
            {
                Policy = MessageTimeoutPolicy.PushQueue,
                MessageDuration = 1,
                TargetName = targetName
            };
        });
    }

    private static HorseMessage CreateMessage(string target, string id)
    {
        HorseMessage message = new(MessageType.QueueMessage, target);
        message.SetMessageId(id);
        message.SetStringContent("timeout-payload");
        return message;
    }

    private static async Task<bool> WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(12);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        return condition();
    }

    private sealed class TimeoutErrorHandler : IErrorHandler
    {
        internal TaskCompletionSource<bool> Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
        {
            if (message.StartsWith("CheckMessageTimeout:", StringComparison.Ordinal))
                Failed.TrySetResult(true);
        }
    }
}
