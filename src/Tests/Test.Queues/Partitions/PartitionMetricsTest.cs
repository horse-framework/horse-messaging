using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Common;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for QueueInfo partition metrics.
/// </summary>
public class PartitionMetricsTest
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(HorseRider rider, int port, HorseQueue queue)> CreateSimpleQueue(
        int maxPartitions = 10,
        string name = "met-q")
    {
        var (rider, port, server) = await PartitionTestServer.Create();

        await rider.Queue.Create(name, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = rider.Queue.Find(name);
        return (rider, port, queue);
    }

    // ── GetMetrics ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMetrics_NoPartitions_ReturnsEmpty()
    {
        var (_, _, queue) = await CreateSimpleQueue();
        var snapshots = queue.PartitionManager.GetMetrics().ToList();
        Assert.Empty(snapshots);
    }

    [Fact]
    public async Task GetMetrics_AfterSubscribe_ContainsPartitionEntry()
    {
        var (_, port, queue) = await CreateSimpleQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("met-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });

        await Task.Delay(400);

        var snapshots = queue.PartitionManager.GetMetrics().ToList();
        Assert.NotEmpty(snapshots);

        PartitionMetricSnapshot labelSnap = snapshots.FirstOrDefault(s => s.Label == "w1");
        Assert.NotNull(labelSnap);
        Assert.Equal(0, labelSnap.MessageCount);
    }

    [Fact]
    public async Task GetMetrics_CorrectMessageCount()
    {
        var (_, port, queue) = await CreateSimpleQueue();

        // Subscribe then disconnect so label partition has no consumer
        HorseClient worker = new HorseClient();
        worker.AutoAcknowledge = false;
        await worker.ConnectAsync("horse://localhost:" + port);
        await worker.Queue.Subscribe("met-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "count-w") });

        await Task.Delay(300);
        worker.Disconnect();
        await Task.Delay(300);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("met-q", Encoding.UTF8.GetBytes("m" + i),
                false, new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "count-w") });

        await Task.Delay(600);

        var snapshots = queue.PartitionManager.GetMetrics().ToList();
        int totalMessages = snapshots.Sum(s => s.MessageCount);
        Assert.True(totalMessages >= 3, $"Expected >= 3 stored messages, got {totalMessages}");
    }

    [Fact]
    public async Task GetMetrics_MultiplePartitions_AllRepresented()
    {
        var (_, port, queue) = await CreateSimpleQueue(maxPartitions: 5);

        for (int i = 1; i <= 3; i++)
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("met-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"w{i}") });
        }

        await Task.Delay(400);

        var snapshots = queue.PartitionManager.GetMetrics().ToList();
        Assert.Equal(3, snapshots.Count);
    }

    // ── RefreshPartitionMetrics on QueueInfo ──────────────────────────────────

    [Fact]
    public async Task QueueInfo_RefreshPartitionMetrics_UpdatesPartitionCount()
    {
        var (_, port, queue) = await CreateSimpleQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("met-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "info-w") });

        await Task.Delay(300);

        queue.Info.RefreshPartitionMetrics(queue.PartitionManager);

        Assert.Equal(1, queue.Info.PartitionCount);
        Assert.NotEmpty(queue.Info.PartitionMetrics);
    }

    [Fact]
    public async Task QueueInfo_NonPartitioned_PartitionCountIsZero()
    {
        var server = new TestHorseRider();
        await server.Initialize();
        server.Start(300, 300);

        HorseQueue queue = server.Rider.Queue.Find("push-a");
        Assert.NotNull(queue);
        Assert.Equal(0, queue.Info.PartitionCount);
        Assert.Empty(queue.Info.PartitionMetrics);

        server.Stop();
    }

    // ── CreatedAt / LastMessageAt ─────────────────────────────────────────────

    [Fact]
    public async Task PartitionEntry_CreatedAt_IsReasonablyRecent()
    {
        var (_, port, queue) = await CreateSimpleQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("met-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "ts-w") });

        await Task.Delay(200);

        PartitionEntry entry = queue.PartitionManager.Partitions.First();
        Assert.True((DateTime.UtcNow - entry.CreatedAt).TotalSeconds < 10);
        Assert.Null(entry.LastMessageAt);
    }

    [Fact]
    public async Task PartitionEntry_LastMessageAt_SetAfterMessageRouted()
    {
        var (_, port, queue) = await CreateSimpleQueue();

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + port);
        await producer.ConnectAsync("horse://localhost:" + port);

        await worker.Queue.Subscribe("met-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "ts2") });
        await Task.Delay(800);

        PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "ts2");
        Assert.NotNull(entry);

        await producer.Queue.Push("met-q", Encoding.UTF8.GetBytes("ts-msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "ts2") });
        await Task.Delay(700);

        Assert.NotNull(entry.LastMessageAt);
    }
}
