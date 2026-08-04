using System.Collections.Generic;
using System.Linq;
using System.Text;
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
/// Tests that consumer-declared subscribers-per-partition capacity works regardless of
/// which side created the label partition first. A partition created publisher-first
/// (push with PARTITION_LABEL before any consumer subscribed — e.g. after idle auto-destroy)
/// used to be locked to the server default capacity, rejecting every consumer beyond the
/// first with LimitExceeded. Consumers must be able to upgrade a live partition's capacity,
/// and the last declared capacity per label must be remembered for future re-creations.
/// </summary>
public class PartitionPublisherFirstCapacityTest
{
    private static Task CreatePartitionedQueue(TestHorseRider server, string name, int autoDestroyIdleSeconds)
    {
        return server.Rider.Queue.Create(name, opts =>
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
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoDestroyIdleSeconds = autoDestroyIdleSeconds
            };
        });
    }

    [Fact]
    public async Task PublisherFirst_ConsumerOverride_UpgradesLivePartition_AllSubscribe()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await CreatePartitionedQueue(server, "pf-upgrade-q", 60);

            // Publisher creates the label partition first — capacity falls back to server default (1)
            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            await producer.Queue.Push("pf-upgrade-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "premium") }, CancellationToken.None);

            await Task.Delay(300);

            HorseQueue queue = server.Rider.Queue.Find("pf-upgrade-q");
            PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "premium");
            Assert.NotNull(entry);
            Assert.Equal(1, entry.Queue.Options.ClientLimit);

            // Three consumers declare capacity 64 — all must join the existing partition
            for (int i = 0; i < 3; i++)
            {
                HorseClient consumer = new HorseClient();
                await consumer.ConnectAsync("horse://localhost:" + port);
                HorseResult result = await consumer.Queue.SubscribePartitioned("pf-upgrade-q", "premium", true, 0, 64, CancellationToken.None);
                Assert.Equal(HorseResultCode.Ok, result.Code);
            }

            await Task.Delay(300);

            Assert.Equal(64, entry.Queue.Options.ClientLimit);
            Assert.Equal(3, entry.Queue.ClientsCount());
        });
    }

    [Fact]
    public async Task RecreateAfterDestroy_UsesRememberedLabelCapacity_And_ReassignsPooledWorkers()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await CreatePartitionedQueue(server, "pf-remember-q", 1);

            // Consumers declare capacity 8 (consumer-first creation)
            HorseClient c1 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            HorseResult r1 = await c1.Queue.SubscribePartitioned("pf-remember-q", "premium", true, 0, 8, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            HorseClient c2 = new HorseClient();
            await c2.ConnectAsync("horse://localhost:" + port);
            HorseResult r2 = await c2.Queue.SubscribePartitioned("pf-remember-q", "premium", true, 0, 8, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r2.Code);

            HorseQueue queue = server.Rider.Queue.Find("pf-remember-q");

            // Empty partition is auto-destroyed (NoMessages, 1s timer); workers return to the pool
            await Task.Delay(3000);
            Assert.Null(queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "premium"));

            // Publisher re-creates the partition — remembered capacity (8) must win over default (1)
            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            await producer.Queue.Push("pf-remember-q", Encoding.UTF8.GetBytes("m1"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "premium") }, CancellationToken.None);
            await producer.Queue.Push("pf-remember-q", Encoding.UTF8.GetBytes("m2"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "premium") }, CancellationToken.None);

            await Task.Delay(500);

            PartitionEntry recreated = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "premium");
            Assert.NotNull(recreated);
            Assert.Equal(8, recreated.Queue.Options.ClientLimit);

            // Both pooled workers must be re-assigned (old behaviour capped auto-assign at the
            // parent-level default of 1 subscriber even when the partition had more capacity)
            Assert.Equal(2, recreated.Queue.ClientsCount());
        });
    }

    [Fact]
    public async Task PublisherFirst_NoConsumerDeclaration_KeepsServerDefault()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await CreatePartitionedQueue(server, "pf-tenant-q", 60);

            // Publisher-first creation for a tenant label
            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            await producer.Queue.Push("pf-tenant-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-a") }, CancellationToken.None);

            await Task.Delay(300);

            // First consumer without a capacity declaration joins
            HorseClient c1 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            HorseResult r1 = await c1.Queue.Subscribe("pf-tenant-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-a") }, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            // Second consumer without declaration is rejected — tenant isolation intact
            HorseClient c2 = new HorseClient();
            await c2.ConnectAsync("horse://localhost:" + port);
            HorseResult r2 = await c2.Queue.Subscribe("pf-tenant-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-a") }, CancellationToken.None);
            Assert.Equal(HorseResultCode.LimitExceeded, r2.Code);

            HorseQueue queue = server.Rider.Queue.Find("pf-tenant-q");
            PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "tenant-a");
            Assert.NotNull(entry);
            Assert.Equal(1, entry.Queue.Options.ClientLimit);
        });
    }

    [Fact]
    public async Task LowerDeclaration_DoesNotShrinkLivePartition_ButAppliesOnRecreate()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await CreatePartitionedQueue(server, "pf-shrink-q", 1);

            HorseClient c1 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            HorseResult r1 = await c1.Queue.SubscribePartitioned("pf-shrink-q", "premium", true, 0, 2, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            HorseClient c2 = new HorseClient();
            await c2.ConnectAsync("horse://localhost:" + port);
            HorseResult r2 = await c2.Queue.SubscribePartitioned("pf-shrink-q", "premium", true, 0, 2, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r2.Code);

            HorseQueue queue = server.Rider.Queue.Find("pf-shrink-q");
            PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "premium");
            Assert.NotNull(entry);
            Assert.Equal(2, entry.Queue.Options.ClientLimit);

            // A lower declaration must not shrink the live partition (2 subscribers connected)
            HorseClient c3 = new HorseClient();
            await c3.ConnectAsync("horse://localhost:" + port);
            HorseResult r3 = await c3.Queue.SubscribePartitioned("pf-shrink-q", "premium", true, 0, 1, CancellationToken.None);
            Assert.Equal(HorseResultCode.LimitExceeded, r3.Code);
            Assert.Equal(2, entry.Queue.Options.ClientLimit);
            Assert.Equal(2, entry.Queue.ClientsCount());

            // Disconnect subscribers so the label memory (last declared = 1) drives the re-create
            c1.Disconnect();
            c2.Disconnect();
            c3.Disconnect();

            await Task.Delay(3000);
            Assert.Null(queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "premium"));

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            await producer.Queue.Push("pf-shrink-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "premium") }, CancellationToken.None);

            await Task.Delay(500);

            PartitionEntry recreated = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "premium");
            Assert.NotNull(recreated);
            Assert.Equal(1, recreated.Queue.Options.ClientLimit);
        });
    }
}
