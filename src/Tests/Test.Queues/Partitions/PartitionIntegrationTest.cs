using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Integration-level end-to-end tests: FetchOrders-style scenario.
/// Uses PartitionTestServer (simple CommitWhen.None) to avoid delivery-handler
/// complexity interfering with partition routing behaviour.
/// </summary>
public class PartitionIntegrationTest
{
    private static async Task<(PartitionTestContext ctx, HorseQueue queue)> CreateServer(
        string queueName,
        int maxPartitions = 10,
        int subscribersPerPartition = 1)
    {
        var (rider, port, server) = await PartitionTestServer.Create();
        string dataPath = $"pt-int-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";
        var ctx = new PartitionTestContext(rider, port, dataPath, server);

        await rider.Queue.Create(queueName, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = rider.Queue.Find(queueName);
        return (ctx, queue);
    }

    [Fact]
    public async Task TenWorkers_TenPartitions_EachReceivesOwnMessages()
    {
        var (ctx, queue) = await CreateServer("FetchOrders");
        await using var _ = ctx;

        int[] received = new int[10];

        for (int i = 0; i < 10; i++)
        {
            int idx = i;
            var client = new HorseClient();
            client.AutoAcknowledge = true;
            await client.ConnectAsync("horse://localhost:" + ctx.Port);
            client.MessageReceived += (_, _) => Interlocked.Increment(ref received[idx]);
            await client.Queue.Subscribe("FetchOrders", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"worker-{i}") }, CancellationToken.None);
        }

        await Task.Delay(800);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        for (int i = 0; i < 10; i++)
            for (int m = 0; m < 5; m++)
                await producer.Queue.Push("FetchOrders", Encoding.UTF8.GetBytes($"order-{m}"), false,
                    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"worker-{i}") }, CancellationToken.None);

        await Task.Delay(3000);

        for (int i = 0; i < 10; i++)
            Assert.True(received[i] >= 5, $"worker-{i} received {received[i]}, expected >= 5");

        Assert.Equal(10, queue.PartitionManager.Partitions.Count());
    }

    [Fact]
    public async Task ProduceToParentQueue_MessagesDistributedToCorrectPartitions()
    {
        var (ctx, queue) = await CreateServer("Orders");
        await using var _ = ctx;

        int receivedA = 0, receivedB = 0;
        HorseClient wA = new HorseClient();
        HorseClient wB = new HorseClient();
        wA.AutoAcknowledge = true;
        wB.AutoAcknowledge = true;
        await wA.ConnectAsync("horse://localhost:" + ctx.Port);
        await wB.ConnectAsync("horse://localhost:" + ctx.Port);
        wA.MessageReceived += (_, _) => Interlocked.Increment(ref receivedA);
        wB.MessageReceived += (_, _) => Interlocked.Increment(ref receivedB);

        await wA.Queue.Subscribe("Orders", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "partA") }, CancellationToken.None);
        await wB.Queue.Subscribe("Orders", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "partB") }, CancellationToken.None);

        await Task.Delay(500);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        for (int i = 0; i < 4; i++)
            await producer.Queue.Push("Orders", Encoding.UTF8.GetBytes($"msg-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, i % 2 == 0 ? "partA" : "partB") }, CancellationToken.None);

        await Task.Delay(1500);

        Assert.True(receivedA >= 2, $"wA expected >= 2, actual {receivedA}");
        Assert.True(receivedB >= 2, $"wB expected >= 2, actual {receivedB}");
    }

    [Fact]
    public async Task WorkersJoinDynamically_NewPartitionOpenedPerWorker()
    {
        var (ctx, queue) = await CreateServer("DynQ", maxPartitions: 0);
        await using var _ = ctx;

        for (int i = 0; i < 3; i++)
        {
            var client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + ctx.Port);
            await client.Queue.Subscribe("DynQ", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"dyn-{i}") }, CancellationToken.None);
            await Task.Delay(100);
        }

        await Task.Delay(300);

        int normalPartitions = queue.PartitionManager.Partitions.Count();
        Assert.Equal(3, normalPartitions);
    }

    [Fact]
    public async Task AllPartitionIds_AreUnique()
    {
        var (ctx, queue) = await CreateServer("UniqueQ", maxPartitions: 0);
        await using var _ = ctx;

        const int n = 20;
        for (int i = 0; i < n; i++)
        {
            var client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + ctx.Port);
            await client.Queue.Subscribe("UniqueQ", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"ul-{i}") }, CancellationToken.None);
        }

        await Task.Delay(500);

        var ids = queue.PartitionManager.Partitions
            .Select(p => p.PartitionId)
            .ToList();

        Assert.Equal((int)ids.Count, (int)ids.Distinct().Count());
    }

    [Fact]
    public async Task PartitionQueueNames_FollowNamingConvention()
    {
        var (ctx, queue) = await CreateServer("NamingQ");
        await using var _ = ctx;

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + ctx.Port);
        await client.Queue.Subscribe("NamingQ", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "naming-w") }, CancellationToken.None);

        await Task.Delay(200);

        foreach (var entry in queue.PartitionManager.Partitions)
            Assert.StartsWith("NamingQ-Partition-", (string)entry.Queue.Name);
    }

    [Fact]
    public async Task DestroyParentQueue_AllPartitionsAlsoDestroyed()
    {
        var (ctx, queue) = await CreateServer("TeardownQ");
        await using var _ = ctx;

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + ctx.Port);
        await client.Queue.Subscribe("TeardownQ", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "td-w") }, CancellationToken.None);

        await Task.Delay(200);
        Assert.NotEmpty(queue.PartitionManager.Partitions);

        string labelPartName = queue.PartitionManager.Partitions
            .FirstOrDefault()?.Queue.Name;

        await ctx.Rider.Queue.Remove(queue);
        await Task.Delay(300);

        Assert.True((bool)queue.IsDestroyed);
        if (labelPartName != null)
            Assert.Null(ctx.Rider.Queue.Find(labelPartName));
    }

    [Fact]
    public async Task CreateQueue_WithPartitionOptions_ServerQueuesHasPartitionEnabled()
    {
        var (rider, _, server) = await PartitionTestServer.Create();

        await rider.Queue.Create("ServerPartQ", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 5,
                SubscribersPerPartition = 1,
            };
        });

        HorseQueue q = rider.Queue.Find("ServerPartQ");
        Assert.NotNull(q);
        Assert.True((bool)q.IsPartitioned);
        Assert.NotNull(q.Options.Partition);
        Assert.True((bool)q.Options.Partition.Enabled);
        Assert.Equal(5, q.Options.Partition.MaxPartitionCount);
        Assert.Equal(1, q.Options.Partition.SubscribersPerPartition);
        await server.StopAsync();
    }
}
