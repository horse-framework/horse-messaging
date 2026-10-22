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

public class PartitionCapacityAndAdoptionTest
{
    private static async Task<HorseClient> Connect(int port)
    {
        HorseClient client = new HorseClient();
        client.AutoSubscribe = false;
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        return client;
    }

    /// <summary>
    /// Raising a live partition to unlimited (0) used to set the limit without growing the
    /// client slot array, so the consumer that asked for unlimited capacity got LimitExceeded.
    /// </summary>
    [Fact]
    public async Task ConsumerDeclaringUnlimited_JoinsFullPartition()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("cap-queue", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    SubscribersPerPartition = 1,
                    AutoAssignWorkers = true,
                    AutoDestroy = PartitionAutoDestroy.Disabled
                };
            });

            HorseQueue queue = server.Rider.Queue.Find("cap-queue");

            for (int i = 0; i < 2; i++)
            {
                HorseClient c = await Connect(port);
                HorseResult r = await c.Queue.SubscribePartitioned("cap-queue", "standard", true, 0, 2, CancellationToken.None);
                Assert.Equal(HorseResultCode.Ok, r.Code);
            }

            // SubscribePartitioned treats 0 as "not specified" and sends no header, so the
            // unlimited declaration is sent as a raw header here.
            HorseClient third = await Connect(port);
            HorseResult result = await third.Queue.Subscribe("cap-queue", true, new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "standard"),
                new KeyValuePair<string, string>(HorseHeaders.PARTITION_SUBSCRIBERS, "0")
            }, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            PartitionEntry entry = queue.PartitionManager.Partitions.Single();
            Assert.Equal(0, entry.Queue.Options.ClientLimit);
            Assert.Equal(3, entry.Queue.ClientsCount());
        });
    }

    /// <summary>
    /// An unlimited plain queue starts with 32 client slots and must grow past them.
    /// </summary>
    [Fact]
    public async Task UnlimitedQueue_AcceptsMoreThanInitialSlots()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("wide-queue", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.ClientLimit = 0;
            });

            HorseQueue queue = server.Rider.Queue.Find("wide-queue");
            List<HorseClient> clients = new();

            for (int i = 0; i < 40; i++)
            {
                HorseClient c = await Connect(port);
                clients.Add(c);
                HorseResult r = await c.Queue.Subscribe("wide-queue", true, CancellationToken.None);
                Assert.Equal(HorseResultCode.Ok, r.Code);
            }

            Assert.Equal(40, queue.ClientsCount());
            Assert.Equal(40, queue.Clients.Select(c => c.Index).Distinct().Count());
        });
    }

    /// <summary>
    /// A queue with the partition sub-queue name that owns a PartitionManager of its own, as replica
    /// copies built by older versions do. When the parent creates that partition it gets the existing
    /// queue back; without repair, every message routed to it was dropped as NoConsumers.
    /// </summary>
    [Fact]
    public async Task ParentAdoptsQueueWithOwnPartitionManager_AndStoresMessages()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            PartitionOptions partition = new PartitionOptions
            {
                Enabled = true,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };

            HorseQueue zombie = await server.Rider.Queue.Create("cargo-queue-Partition-tenant1", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.Partition = partition;
            });
            Assert.True(zombie.IsPartitioned);

            HorseQueue parent = await server.Rider.Queue.Create("cargo-queue", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.Partition = partition;
            });

            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "cargo-queue");
            msg.SetStringContent("cargo");
            msg.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant1");

            Assert.Equal(PushResult.Success, await parent.Push(msg));

            PartitionEntry entry = parent.PartitionManager.Partitions.Single();
            Assert.Same(zombie, entry.Queue);
            Assert.False(zombie.IsPartitioned);
            Assert.True(zombie.IsPartitionQueue);
            Assert.Equal(1, zombie.Manager.MessageStore.Count());
        });
    }
}
