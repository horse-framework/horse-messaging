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
/// Tests that client-provided SubscribersPerPartition header overrides server default
/// for label-based partitions. This is critical for tier-based scaling where the server
/// default is 1 (for tenant isolation) but non-tenant label partitions need multiple
/// subscribers per partition.
/// </summary>
public class PartitionClientOverrideSubscribersTest
{
    /// <summary>
    /// Server default SubscribersPerPartition=1.
    /// Client sends subscribersPerPartition=10 in subscribe headers.
    /// Two clients subscribe to same label — both should succeed.
    /// Currently FAILS: second client gets LimitExceeded because partition sub-queue
    /// is created with server default ClientLimit=1, ignoring client header.
    /// </summary>
    [Fact]
    public async Task Subscribe_ClientOverridesSubscribersPerPartition_SecondClientSucceeds()
    {
        // Server default: SubscribersPerPartition = 1 (tenant isolation default)
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create("tier-queue", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 0,
                MaxPartitionsPerWorker = 1,
                SubscribersPerPartition = 1, // Server default: 1 subscriber per partition
                AutoAssignWorkers = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queue = server.Rider.Queue.Find("tier-queue");
        Assert.NotNull(queue);
        Assert.True(queue.IsPartitioned);

        // Client 1 subscribes with label "free" and requests subscribersPerPartition=10
        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);
        Assert.True(c1.IsConnected);

        HorseResult r1 = await c1.Queue.SubscribePartitioned(
            "tier-queue",
            "free",
            true,
            0,   // maxPartitions: unlimited
            10,  // subscribersPerPartition: client requests 10
            CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, r1.Code);

        await Task.Delay(200);

        // Verify partition was created
        PartitionEntry freePartition = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "free");
        Assert.NotNull(freePartition);

        // Client 2 subscribes to SAME label "free" — should succeed because client requested 10 subscribers
        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync("horse://localhost:" + port);
        Assert.True(c2.IsConnected);

        HorseResult r2 = await c2.Queue.SubscribePartitioned(
            "tier-queue",
            "free",
            true,
            0,
            10,
            CancellationToken.None);

        // THIS IS THE ASSERTION THAT SHOULD PASS BUT CURRENTLY FAILS
        // Because partition sub-queue was created with ClientLimit=1 (server default)
        // instead of respecting client's subscribersPerPartition=10
        Assert.Equal(HorseResultCode.Ok, r2.Code);

        await Task.Delay(200);

        // Both clients should be subscribed to the same "free" partition
        int clientCount = freePartition.Queue.ClientsCount();
        Assert.Equal(2, clientCount);

        server.Stop();
    }

    /// <summary>
    /// When client does NOT send subscribersPerPartition header (no override),
    /// server default SubscribersPerPartition=1 applies.
    /// Second client subscribing to same label should get LimitExceeded.
    /// </summary>
    [Fact]
    public async Task Subscribe_NoOverride_SecondClientSameLabelGetsLimitExceeded()
    {
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create("no-override-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 0,
                SubscribersPerPartition = 1, // Server default: 1
                AutoAssignWorkers = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages
            };
        });

        int port = server.Start(300, 300);

        // Client 1 subscribes with label "free" — NO subscribersPerPartition header
        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);

        HorseResult r1 = await c1.Queue.Subscribe("no-override-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "free") },
            CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r1.Code);

        await Task.Delay(200);

        // Client 2 subscribes to same label "free" — NO override, server default 1 applies
        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync("horse://localhost:" + port);

        HorseResult r2 = await c2.Queue.Subscribe("no-override-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "free") },
            CancellationToken.None);

        // Should be rejected — no override means server default of 1 subscriber
        Assert.Equal(HorseResultCode.LimitExceeded, r2.Code);

        server.Stop();
    }

    /// <summary>
    /// Verifies that tenant-style partitions (different labels per client) still work
    /// with SubscribersPerPartition=1 — each tenant label gets its own partition
    /// with exactly 1 subscriber.
    /// </summary>
    [Fact]
    public async Task Subscribe_TenantIsolation_DifferentLabels_OneSubscriberEach()
    {
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create("tenant-queue", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 0,
                SubscribersPerPartition = 1,
                AutoAssignWorkers = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queue = server.Rider.Queue.Find("tenant-queue");

        // Tenant A subscribes with label "tenant-a"
        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);

        HorseResult r1 = await c1.Queue.Subscribe("tenant-queue", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-a") },
            CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r1.Code);

        // Tenant B subscribes with label "tenant-b" — different partition, should succeed
        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync("horse://localhost:" + port);

        HorseResult r2 = await c2.Queue.Subscribe("tenant-queue", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-b") },
            CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r2.Code);

        // Second client tries tenant-a label — should be rejected (1 subscriber limit)
        HorseClient c3 = new HorseClient();
        await c3.ConnectAsync("horse://localhost:" + port);

        HorseResult r3 = await c3.Queue.Subscribe("tenant-queue", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-a") },
            CancellationToken.None);
        Assert.Equal(HorseResultCode.LimitExceeded, r3.Code);

        await Task.Delay(200);

        // Two partitions: tenant-a and tenant-b
        Assert.Equal(2, queue.PartitionManager.Partitions.Count());

        server.Stop();
    }

    /// <summary>
    /// Mixed scenario: same queue has both tier partitions (multiple subscribers)
    /// and tenant partitions (single subscriber). Client-provided subscribersPerPartition
    /// should override only for the partitions where it's specified.
    /// </summary>
    [Fact]
    public async Task Subscribe_MixedTierAndTenant_RespectsBothLimits()
    {
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create("mixed-queue", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 0,
                SubscribersPerPartition = 1, // Default: 1 (for tenant isolation)
                AutoAssignWorkers = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queue = server.Rider.Queue.Find("mixed-queue");

        // --- Tier partition: "free" with subscribersPerPartition=10 ---

        HorseClient tier1 = new HorseClient();
        await tier1.ConnectAsync("horse://localhost:" + port);
        HorseResult tr1 = await tier1.Queue.SubscribePartitioned("mixed-queue", "free", true, 0, 10, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, tr1.Code);

        HorseClient tier2 = new HorseClient();
        await tier2.ConnectAsync("horse://localhost:" + port);
        HorseResult tr2 = await tier2.Queue.SubscribePartitioned("mixed-queue", "free", true, 0, 10, CancellationToken.None);
        // Should succeed — client requested 10 subscribers
        Assert.Equal(HorseResultCode.Ok, tr2.Code);

        // --- Tenant partition: "tenant-xyz" with default 1 subscriber ---

        HorseClient tenant1 = new HorseClient();
        await tenant1.ConnectAsync("horse://localhost:" + port);
        HorseResult tn1 = await tenant1.Queue.Subscribe("mixed-queue", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-xyz") },
            CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, tn1.Code);

        HorseClient tenant2 = new HorseClient();
        await tenant2.ConnectAsync("horse://localhost:" + port);
        HorseResult tn2 = await tenant2.Queue.Subscribe("mixed-queue", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-xyz") },
            CancellationToken.None);
        // Should be rejected — tenant partition uses server default of 1
        Assert.Equal(HorseResultCode.LimitExceeded, tn2.Code);

        await Task.Delay(200);

        // "free" partition has 2 clients, "tenant-xyz" has 1
        PartitionEntry freeEntry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "free");
        PartitionEntry tenantEntry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "tenant-xyz");

        Assert.NotNull(freeEntry);
        Assert.NotNull(tenantEntry);
        Assert.Equal(2, freeEntry.Queue.ClientsCount());
        Assert.Equal(1, tenantEntry.Queue.ClientsCount());

        server.Stop();
    }
}
