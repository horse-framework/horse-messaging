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
    [Fact]
    public async Task Subscribe_ClientOverridesSubscribersPerPartition_SecondClientSucceeds()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("tier-queue", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 1,
                    SubscribersPerPartition = 1,
                    AutoAssignWorkers = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages
                };
            });

            HorseQueue queue = server.Rider.Queue.Find("tier-queue");
            Assert.NotNull(queue);
            Assert.True(queue.IsPartitioned);

            HorseClient c1 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            Assert.True(c1.IsConnected);

            HorseResult r1 = await c1.Queue.SubscribePartitioned("tier-queue", "free", true, 0, 10, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            await Task.Delay(200);

            PartitionEntry freePartition = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "free");
            Assert.NotNull(freePartition);

            HorseClient c2 = new HorseClient();
            await c2.ConnectAsync("horse://localhost:" + port);
            Assert.True(c2.IsConnected);

            HorseResult r2 = await c2.Queue.SubscribePartitioned("tier-queue", "free", true, 0, 10, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r2.Code);

            await Task.Delay(200);

            int clientCount = freePartition.Queue.ClientsCount();
            Assert.Equal(2, clientCount);
        });
    }

    [Fact]
    public async Task Subscribe_NoOverride_SecondClientSameLabelGetsLimitExceeded()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("no-override-q", opts =>
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

            HorseClient c1 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);

            HorseResult r1 = await c1.Queue.Subscribe("no-override-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "free") },
                CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            await Task.Delay(200);

            HorseClient c2 = new HorseClient();
            await c2.ConnectAsync("horse://localhost:" + port);

            HorseResult r2 = await c2.Queue.Subscribe("no-override-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "free") },
                CancellationToken.None);
            Assert.Equal(HorseResultCode.LimitExceeded, r2.Code);
        });
    }

    [Fact]
    public async Task Subscribe_TenantIsolation_DifferentLabels_OneSubscriberEach()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
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

            HorseQueue queue = server.Rider.Queue.Find("tenant-queue");

            HorseClient c1 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);

            HorseResult r1 = await c1.Queue.Subscribe("tenant-queue", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-a") },
                CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            HorseClient c2 = new HorseClient();
            await c2.ConnectAsync("horse://localhost:" + port);

            HorseResult r2 = await c2.Queue.Subscribe("tenant-queue", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-b") },
                CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r2.Code);

            HorseClient c3 = new HorseClient();
            await c3.ConnectAsync("horse://localhost:" + port);

            HorseResult r3 = await c3.Queue.Subscribe("tenant-queue", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-a") },
                CancellationToken.None);
            Assert.Equal(HorseResultCode.LimitExceeded, r3.Code);

            await Task.Delay(200);

            Assert.Equal(2, queue.PartitionManager.Partitions.Count());
        });
    }

    [Fact]
    public async Task Subscribe_MixedTierAndTenant_RespectsBothLimits()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("mixed-queue", opts =>
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

            HorseQueue queue = server.Rider.Queue.Find("mixed-queue");

            HorseClient tier1 = new HorseClient();
            await tier1.ConnectAsync("horse://localhost:" + port);
            HorseResult tr1 = await tier1.Queue.SubscribePartitioned("mixed-queue", "free", true, 0, 10, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, tr1.Code);

            HorseClient tier2 = new HorseClient();
            await tier2.ConnectAsync("horse://localhost:" + port);
            HorseResult tr2 = await tier2.Queue.SubscribePartitioned("mixed-queue", "free", true, 0, 10, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, tr2.Code);

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
            Assert.Equal(HorseResultCode.LimitExceeded, tn2.Code);

            await Task.Delay(200);

            PartitionEntry freeEntry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "free");
            PartitionEntry tenantEntry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "tenant-xyz");

            Assert.NotNull(freeEntry);
            Assert.NotNull(tenantEntry);
            Assert.Equal(2, freeEntry.Queue.ClientsCount());
            Assert.Equal(1, tenantEntry.Queue.ClientsCount());
        });
    }
}
