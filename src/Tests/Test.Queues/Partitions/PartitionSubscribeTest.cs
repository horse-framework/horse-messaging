using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private static async Task RunWithPartitionedQueue(
        Func<TestHorseRider, int, HorseQueue, Task> action,
        string queueName = "part-q",
        int maxPartitions = 5,
        int subscribersPerPartition = 1,
        QueueAckDecision ack = QueueAckDecision.None)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
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

            HorseQueue queue = server.Rider.Queue.Find(queueName);
            await action(server, port, queue);
        });
    }

    [Fact]
    public async Task Subscribe_WithLabel_CreatesLabelPartition()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);

            HorseResult result = await client.Queue.Subscribe("part-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "worker-1") }, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);

            await Task.Delay(200);

            Assert.NotNull(queue.PartitionManager);
            PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "worker-1");
            Assert.NotNull(entry);
        });
    }

    [Fact]
    public async Task Subscribe_WithLabel_ResponseContainsPartitionHeaders()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult result = await client.Queue.Subscribe("part-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "label-x") }, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.NotNull(queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "label-x"));
        });
    }

    [Fact]
    public async Task Subscribe_SameLabelTwice_SecondClientReturnsLimitExceeded()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient c1 = new HorseClient();
            HorseClient c2 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            await c2.ConnectAsync("horse://localhost:" + port);

            HorseResult r1 = await c1.Queue.Subscribe("part-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "shared-label") }, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            HorseResult r2 = await c2.Queue.Subscribe("part-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "shared-label") }, CancellationToken.None);
            Assert.Equal(HorseResultCode.LimitExceeded, r2.Code);

            await Task.Delay(200);

            int partitionCount = queue.PartitionManager.Partitions.Count();
            Assert.Equal(1, partitionCount);
        }, subscribersPerPartition: 1);
    }

    [Fact]
    public async Task Subscribe_DifferentLabels_CreatesSeparatePartitions()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient c1 = new HorseClient();
            HorseClient c2 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            await c2.ConnectAsync("horse://localhost:" + port);

            await c1.Queue.Subscribe("part-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") }, CancellationToken.None);
            await c2.Queue.Subscribe("part-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w2") }, CancellationToken.None);

            await Task.Delay(200);

            int partitionCount = queue.PartitionManager.Partitions.Count();
            Assert.Equal(2, partitionCount);
        });
    }

    [Fact]
    public async Task Subscribe_NoLabel_CreatesUnlabeledPartition()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult result = await client.Queue.Subscribe("part-q", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            await Task.Delay(200);

            int partitionCount = queue.PartitionManager.Partitions.Count();
            Assert.Equal(1, partitionCount);
        });
    }

    [Fact]
    public async Task Subscribe_NoLabel_MultipleClients_FillsPartitionFirst()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient c1 = new HorseClient();
            HorseClient c2 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            await c2.ConnectAsync("horse://localhost:" + port);

            await c1.Queue.Subscribe("part-q", true, CancellationToken.None);
            await c2.Queue.Subscribe("part-q", true, CancellationToken.None);

            await Task.Delay(200);

            int partitionCount = queue.PartitionManager.Partitions.Count();
            Assert.Equal(2, partitionCount);
        }, subscribersPerPartition: 1);
    }

    [Fact]
    public async Task Subscribe_MaxPartitionsReached_NoLabelSubscriber_ReturnsNull()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient c1 = new HorseClient();
            HorseClient c2 = new HorseClient();
            HorseClient c3 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            await c2.ConnectAsync("horse://localhost:" + port);
            await c3.ConnectAsync("horse://localhost:" + port);

            HorseResult r1 = await c1.Queue.Subscribe("part-q", true, CancellationToken.None);
            HorseResult r2 = await c2.Queue.Subscribe("part-q", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);
            Assert.Equal(HorseResultCode.Ok, r2.Code);

            HorseResult r3 = await c3.Queue.Subscribe("part-q", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.LimitExceeded, r3.Code);

            await Task.Delay(200);

            int partitionCount = queue.PartitionManager.Partitions.Count();
            Assert.Equal(2, partitionCount);
        }, maxPartitions: 2, subscribersPerPartition: 1);
    }

    [Fact]
    public async Task PartitionedQueue_IsPartitioned_True()
    {
        await RunWithPartitionedQueue(async (_, _, queue) => { Assert.True(queue.IsPartitioned); });
    }

    [Fact]
    public async Task NonPartitionedQueue_IsPartitioned_False()
    {
        await TestHorseRider.RunWith(async (server, _) =>
        {
            HorseQueue queue = server.Rider.Queue.Find("push-a");
            Assert.NotNull(queue);
            Assert.False(queue.IsPartitioned);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PartitionQueues_AreMarkedAsPartitionQueue()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("part-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") }, CancellationToken.None);

            await Task.Delay(200);

            foreach (PartitionEntry entry in queue.PartitionManager.Partitions)
            {
                Assert.True(entry.Queue.IsPartitionQueue);
                Assert.False(entry.Queue.IsPartitioned);
            }
        });
    }

    /// <summary>
    /// SubscribePartitioned helper builds PARTITION_LABEL, PARTITION_LIMIT and
    /// PARTITION_SUBSCRIBERS headers automatically.
    /// </summary>
    [Fact]
    public async Task SubscribePartitioned_BuildsHeadersCorrectly()
    {
        await RunWithPartitionedQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);

            HorseResult result = await client.Queue.SubscribePartitioned(
                "part-q",
                "worker-a",
                true,
                8,
                2,
                CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            await Task.Delay(200);

            PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "worker-a");
            Assert.NotNull(entry);
        }, maxPartitions: 8, subscribersPerPartition: 2);
    }

    /// <summary>
    /// When AutoQueueCreation is enabled and the client sends PARTITION_LIMIT +
    /// PARTITION_SUBSCRIBERS headers, the server creates the queue as partitioned.
    /// </summary>
    [Fact]
    public async Task AutoQueueCreation_WithPartitionHeaders_CreatesPartitionedQueue()
    {
        var (rider, port, _) = await PartitionTestServer.Create();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        HorseResult result = await client.Queue.SubscribePartitioned(
            "auto-part-q",
            "w1",
            true,
            5,
            1,
            CancellationToken.None);

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
