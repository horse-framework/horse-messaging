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
/// Regression test: with AutoAssignWorkers and unlimited MaxPartitionsPerWorker, a labeled worker
/// that re-enters the pool after its partition is auto-destroyed must not be added to the
/// re-created partition again and again. Every push with free capacity used to call
/// TryAssignPooledWorker, which re-added the same worker until all slots were taken,
/// leaving no room for the other consumers of that label.
/// </summary>
public class PartitionAutoAssignDuplicateTest
{
    private const int Capacity = 8;

    private static async Task<HorseClient> SubscribeWithLabel(int port, string queue, string label)
    {
        HorseClient client = new HorseClient();
        client.AutoSubscribe = false;
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);

        HorseResult result = await client.Queue.Subscribe(queue, true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) },
            CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, result.Code);
        return client;
    }

    [Fact]
    public async Task RecreatedPartition_DoesNotFillWithSameWorker()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("InvoiceQueue", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages,
                    AutoAssignWorkers = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 0,
                    SubscribersPerPartition = Capacity,
                    AutoDestroyIdleSeconds = 2
                };
            });

            HorseQueue queue = server.Rider.Queue.Find("InvoiceQueue");

            HorseClient first = await SubscribeWithLabel(port, "InvoiceQueue", "standard");

            // Wait for the idle partition to be destroyed; the worker goes back to the pool.
            await Task.Delay(4500);
            Assert.Empty(queue.PartitionManager.Partitions);

            // Publisher-first re-creation: each push sees free capacity and pulls from the pool.
            for (int i = 0; i < Capacity * 2; i++)
            {
                HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "InvoiceQueue");
                msg.SetStringContent("tick");
                msg.AddHeader(HorseHeaders.PARTITION_LABEL, "standard");
                await queue.Push(msg);
            }

            await Task.Delay(500);

            PartitionEntry entry = queue.PartitionManager.Partitions.Single();
            int firstSeats = entry.Queue.Clients.Count(c => c.Client.UniqueId == first.ClientId);
            Assert.Equal(1, firstSeats);

            // Other consumers of the same label must still find a seat.
            HorseClient second = await SubscribeWithLabel(port, "InvoiceQueue", "standard");
            Assert.Equal(2, entry.Queue.Clients.Count());
            Assert.Contains(entry.Queue.Clients, c => c.Client.UniqueId == second.ClientId);
        });
    }

    [Fact]
    public async Task LabeledResubscribe_DoesNotTakeSecondSeat()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("InvoiceQueue", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.Disabled,
                    AutoAssignWorkers = true,
                    MaxPartitionsPerWorker = 0,
                    SubscribersPerPartition = Capacity
                };
            });

            HorseQueue queue = server.Rider.Queue.Find("InvoiceQueue");
            HorseClient client = await SubscribeWithLabel(port, "InvoiceQueue", "standard");

            HorseResult again = await client.Queue.Subscribe("InvoiceQueue", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "standard") },
                CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, again.Code);

            PartitionEntry entry = queue.PartitionManager.Partitions.Single();
            Assert.Single(entry.Queue.Clients);
        });
    }
}
