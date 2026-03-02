using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Common;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for subscribing to partitioned queues.
/// </summary>
public class PartitionSubscribeTest
{
    private static async Task<(TestHorseRider server, int port, HorseQueue queue)> CreatePartitionedQueue(
        string queueName = "part-q",
        int maxPartitions = 5,
        int subscribersPerPartition = 1,
        QueueAckDecision ack = QueueAckDecision.None)
    {
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create(queueName, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = ack;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queue = server.Rider.Queue.Find(queueName);
        return (server, port, queue);
    }

    [Fact]
    public async Task Subscribe_WithLabel_CreatesLabelPartition()
    {
        var (server, port, queue) = await CreatePartitionedQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);

        HorseResult result = await client.Queue.Subscribe("part-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "worker-1") });

        Assert.Equal(HorseResultCode.Ok, result.Code);

        await Task.Delay(200);

        Assert.NotNull(queue.PartitionManager);
        PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "worker-1");
        Assert.NotNull(entry);

        server.Stop();
    }

    [Fact]
    public async Task Subscribe_WithLabel_ResponseContainsPartitionHeaders()
    {
        var (server, port, queue) = await CreatePartitionedQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        HorseResult result = await client.Queue.Subscribe("part-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "label-x") });

        Assert.Equal(HorseResultCode.Ok, result.Code);
        Assert.NotNull(queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "label-x"));

        server.Stop();
    }

    [Fact]
    public async Task Subscribe_SameLabelTwice_SecondClientReturnsLimitExceeded()
    {
        // SubscribersPerPartition=1 means second client with same label gets rejected
        var (server, port, queue) = await CreatePartitionedQueue(subscribersPerPartition: 1);

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);
        await c2.ConnectAsync("horse://localhost:" + port);

        HorseResult r1 = await c1.Queue.Subscribe("part-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "shared-label") });
        Assert.Equal(HorseResultCode.Ok, r1.Code);

        // second same-label client: partition is full → no fallback → LimitExceeded
        HorseResult r2 = await c2.Queue.Subscribe("part-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "shared-label") });
        Assert.Equal(HorseResultCode.LimitExceeded, r2.Code);

        await Task.Delay(200);

        // Only 1 partition created
        int partitionCount = queue.PartitionManager.Partitions.Count();
        Assert.Equal(1, partitionCount);

        server.Stop();
    }

    [Fact]
    public async Task Subscribe_DifferentLabels_CreatesSeparatePartitions()
    {
        var (server, port, queue) = await CreatePartitionedQueue();

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);
        await c2.ConnectAsync("horse://localhost:" + port);

        await c1.Queue.Subscribe("part-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });
        await c2.Queue.Subscribe("part-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w2") });

        await Task.Delay(200);

        int partitionCount = queue.PartitionManager.Partitions.Count();
        Assert.Equal(2, partitionCount);

        server.Stop();
    }

    [Fact]
    public async Task Subscribe_NoLabel_CreatesUnlabeledPartition()
    {
        var (server, port, queue) = await CreatePartitionedQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        HorseResult result = await client.Queue.Subscribe("part-q", true);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        await Task.Delay(200);

        int partitionCount = queue.PartitionManager.Partitions.Count();
        Assert.Equal(1, partitionCount);

        server.Stop();
    }

    [Fact]
    public async Task Subscribe_NoLabel_MultipleClients_FillsPartitionFirst()
    {
        var (server, port, queue) = await CreatePartitionedQueue(subscribersPerPartition: 1);

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);
        await c2.ConnectAsync("horse://localhost:" + port);

        await c1.Queue.Subscribe("part-q", true);
        await c2.Queue.Subscribe("part-q", true);

        await Task.Delay(200);

        int partitionCount = queue.PartitionManager.Partitions.Count();
        Assert.Equal(2, partitionCount);

        server.Stop();
    }

    [Fact]
    public async Task Subscribe_MaxPartitionsReached_NoLabelSubscriber_ReturnsNull()
    {
        // 2 max partitions, 1 subscriber each — 3rd subscriber gets rejected
        var (server, port, queue) = await CreatePartitionedQueue(maxPartitions: 2, subscribersPerPartition: 1);

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        HorseClient c3 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);
        await c2.ConnectAsync("horse://localhost:" + port);
        await c3.ConnectAsync("horse://localhost:" + port);

        // Two no-label subscribers fill up the 2 partitions
        HorseResult r1 = await c1.Queue.Subscribe("part-q", true);
        HorseResult r2 = await c2.Queue.Subscribe("part-q", true);
        Assert.Equal(HorseResultCode.Ok, r1.Code);
        Assert.Equal(HorseResultCode.Ok, r2.Code);

        // 3rd no-label subscriber: max reached → LimitExceeded
        HorseResult r3 = await c3.Queue.Subscribe("part-q", true);
        Assert.Equal(HorseResultCode.LimitExceeded, r3.Code);

        await Task.Delay(200);

        // Still only 2 partitions
        int partitionCount = queue.PartitionManager.Partitions.Count();
        Assert.Equal(2, partitionCount);

        server.Stop();
    }

    [Fact]
    public async Task PartitionedQueue_IsPartitioned_True()
    {
        var (server, port, queue) = await CreatePartitionedQueue();
        Assert.True(queue.IsPartitioned);
        server.Stop();
    }


    [Fact]
    public async Task NonPartitionedQueue_IsPartitioned_False()
    {
        var server = new TestHorseRider();
        await server.Initialize();
        server.Start(300, 300);

        HorseQueue queue = server.Rider.Queue.Find("push-a");
        Assert.NotNull(queue);
        Assert.False(queue.IsPartitioned);

        server.Stop();
    }

    [Fact]
    public async Task PartitionQueues_AreMarkedAsPartitionQueue()
    {
        var (server, port, queue) = await CreatePartitionedQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("part-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });

        await Task.Delay(200);

        foreach (PartitionEntry entry in queue.PartitionManager.Partitions)
        {
            Assert.True(entry.Queue.IsPartitionQueue);
            Assert.False(entry.Queue.IsPartitioned);
        }

        server.Stop();
    }

    /// <summary>
    /// SubscribePartitioned helper builds PARTITION_LABEL, PARTITION_LIMIT and
    /// PARTITION_SUBSCRIBERS headers automatically.
    /// </summary>
    [Fact]
    public async Task SubscribePartitioned_BuildsHeadersCorrectly()
    {
        var (server, port, queue) = await CreatePartitionedQueue(maxPartitions: 8, subscribersPerPartition: 2);

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        // Use the new ergonomic overload
        HorseResult result = await client.Queue.SubscribePartitioned(
            "part-q",
            partitionLabel: "worker-a",
            verifyResponse: true,
            maxPartitions: 8,
            subscribersPerPartition: 2);

        Assert.Equal(HorseResultCode.Ok, result.Code);
        await Task.Delay(200);

        PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "worker-a");
        Assert.NotNull(entry);

        server.Stop();
    }

    /// <summary>
    /// When AutoQueueCreation is enabled and the client sends PARTITION_LIMIT +
    /// PARTITION_SUBSCRIBERS headers, the server creates the queue as partitioned.
    /// </summary>
    [Fact]
    public async Task AutoQueueCreation_WithPartitionHeaders_CreatesPartitionedQueue()
    {
        // Use PartitionTestServer which has AutoQueueCreation=true and no pre-created queues
        var (rider, port, _) = await PartitionTestServer.Create();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        // Queue "auto-part-q" does not exist yet; headers tell the server to create it partitioned
        HorseResult result = await client.Queue.SubscribePartitioned(
            "auto-part-q",
            partitionLabel: "w1",
            verifyResponse: true,
            maxPartitions: 5,
            subscribersPerPartition: 1);

        Assert.Equal(HorseResultCode.Ok, result.Code);
        await Task.Delay(400);

        HorseQueue created = rider.Queue.Find("auto-part-q");
        Assert.NotNull(created);
        Assert.True(created.IsPartitioned);
        Assert.Equal(5, created.Options.Partition.MaxPartitionCount);
        Assert.Equal(1, created.Options.Partition.SubscribersPerPartition);

        PartitionEntry entry = created.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "w1");
        Assert.NotNull(entry);
    }
}

