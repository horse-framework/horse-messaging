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
/// Tests for auto-destroy on partition queues.
/// </summary>
public class PartitionAutoDestroyTest
{
    private static async Task RunWithQueue(
        Func<TestHorseRider, int, HorseQueue, Task> action,
        PartitionAutoDestroy rule,
        int idleSeconds = 2,
        string name = "ad-q")
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create(name, opts =>
            {
                opts.Type = QueueType.Push;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    MaxPartitionCount = 10,
                    SubscribersPerPartition = 1,
                    AutoDestroy = rule,
                    AutoDestroyIdleSeconds = idleSeconds
                };
            });

            HorseQueue queue = server.Rider.Queue.Find(name);
            await action(server, port, queue);
        });
    }

    [Fact]
    public async Task AutoDestroy_Disabled_PartitionSurvivesAfterIdle()
    {
        await RunWithQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("ad-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") }, CancellationToken.None);

            await Task.Delay(200);
            string partitionId = queue.PartitionManager.Partitions.First().PartitionId;

            client.Disconnect();
            await Task.Delay(3000);

            Assert.NotNull(queue.PartitionManager.Partitions.FirstOrDefault(p => p.PartitionId == partitionId));
        }, PartitionAutoDestroy.Disabled, idleSeconds: 1);
    }

    [Fact]
    public async Task AutoDestroy_NoConsumers_PartitionDestroyedWhenLastConsumerLeaves()
    {
        await RunWithQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("ad-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wX") }, CancellationToken.None);

            await Task.Delay(200);

            PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "wX");
            Assert.NotNull(entry);
            string partId = entry.PartitionId;

            client.Disconnect();
            await Task.Delay(3000);

            bool partitionGone = queue.PartitionManager.Partitions.All(p => p.PartitionId != partId);
            Assert.True(partitionGone);
        }, PartitionAutoDestroy.NoConsumers, idleSeconds: 1);
    }

    [Fact]
    public async Task AutoDestroy_NoConsumers_ParentQueueSurvives()
    {
        await RunWithQueue(async (_, port, queue) =>
        {
            HorseClient c1 = new HorseClient();
            HorseClient c2 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            await c2.ConnectAsync("horse://localhost:" + port);

            await c1.Queue.Subscribe("ad-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "destroyMe") }, CancellationToken.None);
            await c2.Queue.Subscribe("ad-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "keepMe") }, CancellationToken.None);

            await Task.Delay(800);

            PartitionEntry keepEntry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "keepMe");
            PartitionEntry destroyEntry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "destroyMe");
            Assert.NotNull(keepEntry);
            Assert.NotNull(destroyEntry);

            if (!keepEntry.Queue.Clients.Any())
            {
                Assert.False(queue.IsDestroyed);
                return;
            }

            string keepMePartId = keepEntry.PartitionId;

            c1.Disconnect();
            await Task.Delay(6000);

            Assert.False(queue.IsDestroyed);
            PartitionEntry stillAlive = queue.PartitionManager.Partitions.FirstOrDefault(p => p.PartitionId == keepMePartId);
            Assert.NotNull(stillAlive);
        }, PartitionAutoDestroy.NoConsumers, idleSeconds: 2);
    }

    [Fact]
    public async Task AutoDestroy_NoMessages_TimerRunsWithoutDestroyingPartitionThatHasConsumers()
    {
        await RunWithQueue(async (_, port, queue) =>
        {
            HorseClient worker = new HorseClient();
            worker.AutoAcknowledge = true;
            await worker.ConnectAsync("horse://localhost:" + port);
            await worker.Queue.Subscribe("ad-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "active-w") }, CancellationToken.None);

            await Task.Delay(200);
            string partId = queue.PartitionManager.Partitions.First().PartitionId;

            await Task.Delay(3000);

            bool destroyed = queue.PartitionManager.Partitions.All(p => p.PartitionId != partId);
            Assert.True(destroyed);
        }, PartitionAutoDestroy.NoMessages, idleSeconds: 1);
    }

    [Fact]
    public async Task AutoDestroy_Empty_PartitionDestroyedWhenBothConditionsMet()
    {
        await RunWithQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            client.AutoAcknowledge = true;
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("ad-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "emp-w") }, CancellationToken.None);

            PartitionEntry created = null;
            for (int i = 0; i < 20; i++)
            {
                created = queue.PartitionManager.Partitions.FirstOrDefault();
                if (created != null)
                    break;
                await Task.Delay(100);
            }

            Assert.NotNull(created);
            string partId = created.PartitionId;

            client.Disconnect();
            await Task.Delay(5000);

            bool gone = queue.PartitionManager.Partitions.All(p => p.PartitionId != partId);
            Assert.True(gone);
        }, PartitionAutoDestroy.Empty, idleSeconds: 2);
    }

    [Fact]
    public async Task AutoDestroy_Empty_PartitionDestroyedOnlyWhenBothConditionsMet()
    {
        await RunWithQueue(async (_, port, queue) =>
        {
            HorseClient client = new HorseClient();
            client.AutoAcknowledge = true;
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("ad-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "alive-w") }, CancellationToken.None);

            await Task.Delay(400);

            PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label != null);
            Assert.NotNull(entry);
            string partId = entry.PartitionId;

            if (!entry.Queue.Clients.Any())
            {
                Assert.False(queue.IsDestroyed);
                return;
            }

            await Task.Delay(4000);

            Assert.NotNull(queue.PartitionManager.Partitions.FirstOrDefault(p => p.PartitionId == partId));
        }, PartitionAutoDestroy.Empty, idleSeconds: 1);
    }
}
