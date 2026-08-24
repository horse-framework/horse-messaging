using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Regression tests for silent message loss caused by the partition auto-destroy reaper.
///
/// <para>
/// PartitionManager.CheckAutoDestroy used to evaluate <c>PartitionAutoDestroy.NoMessages</c> as
/// <c>entry.Queue.IsEmpty</c>, while HorseQueue.CheckAutoDestroy guarded the equivalent rule with
/// <c>GetDeliveryCount() == 0</c>. Two behaviours for one rule, and the partition copy was the
/// unguarded one — and the only one that ran, since QueueOptions.AutoDestroy defaults to Disabled.
/// </para>
///
/// <para>
/// IsEmpty only inspects the message stores. A message is invisible to it during three windows:
/// while the drain loop holds it between ConsumeFirst and Push, while IQueueState.Push waits up to
/// 30 seconds for a free consumer, and while it is sent but not yet acknowledged. Destroying the
/// partition in any of those windows deleted the persistent database and disposed the timers, and
/// the message was then written into the dead store by AddMessage — which reported Success.
/// No error, no dead-letter queue, no log line.
/// </para>
///
/// <para>
/// Observed in production on a per-tenant partitioned queue: over 24 hours, 148 of 688 partition
/// lifecycles lost a message, and 144 of those 148 were destroyed while a delivery was in flight.
/// </para>
/// </summary>
public class PartitionAutoDestroyInFlightTest
{
    /// <summary>
    /// The production shape: RoundRobin, WaitForAcknowledge, one subscriber per partition,
    /// NoMessages auto-destroy. The consumer holds message 0 past several reaper ticks, which
    /// parks message 1 inside Push while it waits for the consumer to free up.
    ///
    /// Before the fix the reaper saw an empty store and destroyed the partition underneath both
    /// of them, and message 1 was never delivered.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Reaper_DoesNotDestroyPartition_WhileDeliveryIsInFlight(string mode)
    {
        await using PartitionTestContext ctx = await PartitionTestServer.Create(mode, o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        await ctx.Rider.Queue.Create("inflight-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoDestroyIdleSeconds = 1
            };
        });

        int received = 0;
        HorseClient consumer = new HorseClient { AutoAcknowledge = false };
        consumer.MessageReceived += (client, message) =>
        {
            Interlocked.Increment(ref received);

            // Hold the delivery across several reaper ticks before acknowledging.
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await ((HorseClient) client).SendAsync(message.CreateAcknowledge(), CancellationToken.None);
            });
        };

        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.SubscribePartitioned("inflight-q", "tenant-a", true, CancellationToken.None);
        await Task.Delay(500);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 2; i++)
            await producer.Queue.Push("inflight-q", Encoding.UTF8.GetBytes($"msg-{i}"), false,
                new[] {new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-a")},
                CancellationToken.None);

        for (int i = 0; i < 200 && Volatile.Read(ref received) < 2; i++)
            await Task.Delay(100);

        Assert.Equal(2, Volatile.Read(ref received));

        producer.Disconnect();
        consumer.Disconnect();
    }

    /// <summary>
    /// The invariant behind the fix, asserted directly: while a delivery is awaiting acknowledge
    /// the store really is empty, so IsEmpty is true — and IsIdleForDestroy must still be false.
    /// </summary>
    [Fact]
    public async Task IsIdleForDestroy_IsFalse_WhileStoreIsEmptyButDeliveryIsInFlight()
    {
        await using PartitionTestContext ctx = await PartitionTestServer.Create("memory", o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        await ctx.Rider.Queue.Create("idle-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        TaskCompletionSource<bool> arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HorseClient consumer = new HorseClient {AutoAcknowledge = false};
        consumer.MessageReceived += (_, _) => arrived.TrySetResult(true);

        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.SubscribePartitioned("idle-q", "tenant-b", true, CancellationToken.None);
        await Task.Delay(500);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("idle-q", Encoding.UTF8.GetBytes("held"), false,
            new[] {new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-b")},
            CancellationToken.None);

        await Task.WhenAny(arrived.Task, Task.Delay(10000));
        Assert.True(arrived.Task.IsCompletedSuccessfully, "consumer never received the message");

        HorseQueue parent = ctx.Rider.Queue.Find("idle-q");
        PartitionEntry partition = parent.PartitionManager.Partitions.First(p => p.Label == "tenant-b");

        // The message is out of the store and sitting in the delivery tracker: this is exactly the
        // state the old IsEmpty-only check read as "safe to destroy".
        Assert.True(partition.Queue.IsEmpty);
        Assert.False(partition.Queue.IsIdleForDestroy);

        producer.Disconnect();
        consumer.Disconnect();
    }

    /// <summary>
    /// A destroyed queue has torn down its manager, deleted its persistent database and disposed
    /// its timers. AddMessage used to accept a message into that dead store and return Success,
    /// which is what made every variant of this race silent. It must fail instead, so the producer
    /// sees HorseResultCode.Failed and can retry.
    /// </summary>
    [Fact]
    public async Task AddMessage_Fails_AfterQueueIsDestroyed()
    {
        await using PartitionTestContext ctx = await PartitionTestServer.CreateContext();

        HorseQueue queue = await ctx.Rider.Queue.Create("dead-q", opts => { opts.Type = QueueType.Push; });
        Assert.NotNull(queue);

        await ctx.Rider.Queue.Remove(queue);
        Assert.True(queue.IsDestroyed);

        QueueMessage message = new QueueMessage(new HorseMessage(MessageType.QueueMessage, "dead-q"));
        PushResult result = queue.AddMessage(message, false);

        Assert.NotEqual(PushResult.Success, result);
    }
}
