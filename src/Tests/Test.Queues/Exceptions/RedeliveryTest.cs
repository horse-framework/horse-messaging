using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Data;
using Horse.Messaging.Data.Implementation;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;
using Microsoft.Extensions.DependencyInjection;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Exceptions;

#region Redelivery Tracker

public class RedeliveryTracker
{
    public readonly ConcurrentBag<(string MessageId, int DeliveryCount, string Data)> Deliveries = new();
    public int ConsumeCount;
    public bool ShouldNack = true;
    public int StopNackingAfterDelivery = int.MaxValue;
}

public class RedeliveryTrackerAccessor(RedeliveryTracker tracker)
{
    public RedeliveryTracker Tracker { get; } = tracker;
}

#endregion

#region Redelivery Models

[QueueName("redeliver-q")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.waitAcknowledge)]
public class RedeliverModel
{
    public string Data { get; set; }
}

#endregion

#region Redelivery Consumers

/// <summary>
/// Consumer that records delivery count from message header and optionally sends NACK.
/// </summary>
[AutoAck]
[AutoNack(NegativeReason.Error)]
public class RedeliveryTrackingConsumer(RedeliveryTrackerAccessor accessor) : IQueueConsumer<RedeliverModel>
{
    public Task Consume(HorseMessage message, RedeliverModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        int count = Interlocked.Increment(ref accessor.Tracker.ConsumeCount);
        string deliveryHeader = message.FindHeader(HorseHeaders.DELIVERY);
        int deliveryCount = string.IsNullOrEmpty(deliveryHeader) ? 1 : int.Parse(deliveryHeader);

        accessor.Tracker.Deliveries.Add((message.MessageId, deliveryCount, model.Data));

        if (accessor.Tracker.ShouldNack && count <= accessor.Tracker.StopNackingAfterDelivery)
            throw new InvalidOperationException("Force NACK for redelivery test");

        return Task.CompletedTask;
    }
}

#endregion

/// <summary>
/// Tests for the Redelivery mechanism in persistent queues.
/// 
/// Redelivery tracking is a persistent-mode feature:
///   - When UseRedelivery=true, each delivery attempt increments DeliveryCount
///   - The count is persisted to a .delivery file alongside the .hdb queue file
///   - On redelivery (DeliveryCount > 1), a "Delivery" header is added to the message
///   - The count survives server restarts
///   - When the message is removed (ACK or delete), the redelivery entry is cleaned up
/// </summary>
public class RedeliveryTest
{
    #region Helpers

    private static async Task<(QueueTestContext ctx, string dataPath)> CreatePersistentWithRedelivery()
    {
        string dataPath = $"qt-redel-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.RoundRobin;
                q.Options.Acknowledge = QueueAckDecision.waitAcknowledge;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.AutoQueueCreation = true;
                q.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
                q.Options.PutBack = PutBackDecision.Regular;

                q.UsePersistentQueues(
                    db => db.UseInstantFlush().SetAutoShrink(false),
                    queue =>
                    {
                        queue.Options.CommitWhen = q.Options.CommitWhen;
                        queue.Options.Acknowledge = q.Options.Acknowledge;
                    },
                    useRedelivery: true);
            })
            .Build();

        var (port, server) = await StartServer(rider);
        var ctx = new QueueTestContext(rider, port, dataPath, server);
        return (ctx, dataPath);
    }

    private static async Task<(int port, HorseServer server)> StartServer(HorseRider rider)
    {
        for (int i = 0; i < 50; i++)
        {
            try
            {
                int port = Random.Shared.Next(10000, 60000);
                var opts = HorseServerOptions.CreateDefault();
                opts.Hosts[0].Port = port;
                opts.PingInterval = 300;
                opts.RequestTimeout = 300;
                var server = new HorseServer(opts);
                server.UseRider(rider);
                server.StartAsync().GetAwaiter().GetResult();
                await Task.Delay(100);
                return (port, server);
            }
            catch
            {
                Thread.Sleep(5);
            }
        }

        await Task.Delay(100);
        return (0, null);
    }

    private static async Task<HorseClient> ConnectRaw(int port)
    {
        HorseClient c = new();
        c.ResponseTimeout = TimeSpan.FromSeconds(10);
        await c.ConnectAsync($"horse://localhost:{port}");
        Assert.True(c.IsConnected);
        return c;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 10_000)
    {
        int elapsed = 0;
        while (!condition() && elapsed < timeoutMs)
        {
            await Task.Delay(50);
            elapsed += 50;
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  1. RedeliveryService unit tests
    // ═══════════════════════════════════════════════════════════════════

    #region RedeliveryService Unit

    [Fact]
    public async Task RedeliveryService_SetAndGet_TracksDeliveryCount()
    {
        string path = $"redel-unit-{Guid.NewGuid()}.delivery";
        try
        {
            RedeliveryService service = new(path);
            await service.Load();

            Assert.Empty(service.GetDeliveries());

            await service.Set("msg-1", 1);
            var deliveries = service.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("msg-1", deliveries[0].Key);
            Assert.Equal(1, deliveries[0].Value);

            await service.Set("msg-1", 2);
            deliveries = service.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal(2, deliveries[0].Value);

            await service.Set("msg-2", 1);
            deliveries = service.GetDeliveries();
            Assert.Equal(2, deliveries.Count);

            await service.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RedeliveryService_Remove_DeletesEntry()
    {
        string path = $"redel-remove-{Guid.NewGuid()}.delivery";
        try
        {
            RedeliveryService service = new(path);
            await service.Load();

            await service.Set("msg-1", 3);
            await service.Set("msg-2", 1);
            Assert.Equal(2, service.GetDeliveries().Count);

            await service.Remove("msg-1");
            var deliveries = service.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("msg-2", deliveries[0].Key);

            await service.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RedeliveryService_Clear_RemovesAll()
    {
        string path = $"redel-clear-{Guid.NewGuid()}.delivery";
        try
        {
            RedeliveryService service = new(path);
            await service.Load();

            await service.Set("msg-1", 1);
            await service.Set("msg-2", 2);
            await service.Set("msg-3", 3);
            Assert.Equal(3, service.GetDeliveries().Count);

            await service.Clear();
            Assert.Empty(service.GetDeliveries());

            await service.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RedeliveryService_PersistAndReload_SurvivesRestart()
    {
        string path = $"redel-persist-{Guid.NewGuid()}.delivery";
        try
        {
            // Write data
            RedeliveryService service1 = new(path);
            await service1.Load();
            await service1.Set("msg-A", 5);
            await service1.Set("msg-B", 2);
            await service1.Close();

            // Reload
            RedeliveryService service2 = new(path);
            await service2.Load();
            var deliveries = service2.GetDeliveries();

            Assert.Equal(2, deliveries.Count);
            Assert.Contains(deliveries, d => d.Key == "msg-A" && d.Value == 5);
            Assert.Contains(deliveries, d => d.Key == "msg-B" && d.Value == 2);

            await service2.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RedeliveryService_UpdateThenReload_LatestValuePersisted()
    {
        string path = $"redel-update-{Guid.NewGuid()}.delivery";
        try
        {
            RedeliveryService service1 = new(path);
            await service1.Load();
            await service1.Set("msg-X", 1);
            await service1.Set("msg-X", 2);
            await service1.Set("msg-X", 3);
            await service1.Close();

            RedeliveryService service2 = new(path);
            await service2.Load();
            var deliveries = service2.GetDeliveries();

            Assert.Single(deliveries);
            Assert.Equal("msg-X", deliveries[0].Key);
            Assert.Equal(3, deliveries[0].Value);

            await service2.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RedeliveryService_Delete_RemovesFile()
    {
        string path = $"redel-delete-{Guid.NewGuid()}.delivery";

        RedeliveryService service = new(path);
        await service.Load();
        await service.Set("msg-1", 1);
        await service.Close();

        Assert.True(File.Exists(path));

        service.Delete();
        Assert.False(File.Exists(path));
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  2. Server-level redelivery: DeliveryCount increments on NACK + PutBack
    // ═══════════════════════════════════════════════════════════════════

    #region Server-Level Redelivery

    [Fact]
    public async Task Redelivery_NackWithPutBack_DeliveryCountIncrements()
    {
        var (ctx, dataPath) = await CreatePersistentWithRedelivery();
        try
        {
            await ctx.Rider.Queue.Create("redeliver-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.waitAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.PutBack = PutBackDecision.Regular;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            });

            int deliveryCountSeen = 0;
            int consumeCount = 0;

            HorseClient consumer = new();
            consumer.ResponseTimeout = TimeSpan.FromSeconds(10);
            consumer.MessageReceived += async (client, msg) =>
            {
                int count = Interlocked.Increment(ref consumeCount);
                string header = msg.FindHeader(HorseHeaders.DELIVERY);
                if (!string.IsNullOrEmpty(header))
                    Interlocked.Exchange(ref deliveryCountSeen, int.Parse(header));

                if (count <= 2)
                {
                    // NACK first 2 deliveries → put-back
                    await client.SendNegativeAck(msg, CancellationToken.None);
                }
                else
                {
                    // ACK on 3rd delivery
                    await client.SendAck(msg, CancellationToken.None);
                }
            };
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("redeliver-q", true, CancellationToken.None);
            await Task.Delay(300);

            HorseClient producer = await ConnectRaw(ctx.Port);
            await producer.Queue.Push("redeliver-q", new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test-data")), false, CancellationToken.None);

            await WaitUntil(() => consumeCount >= 3, 15_000);
            await Task.Delay(500);

            Assert.True(consumeCount >= 3, $"Expected ≥3 deliveries, got {consumeCount}");
            // DeliveryCount=3 means Delivery header = "3" on third delivery
            Assert.True(deliveryCountSeen >= 2, $"Expected delivery header ≥2, got {deliveryCountSeen}");

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.DisposeAsync();
            try { if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Redelivery_FirstDelivery_NoDeliveryHeader()
    {
        var (ctx, dataPath) = await CreatePersistentWithRedelivery();
        try
        {
            await ctx.Rider.Queue.Create("redeliver-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.waitAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.PutBack = PutBackDecision.No;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            });

            string firstDeliveryHeader = "NOT_CHECKED";

            HorseClient consumer = new();
            consumer.AutoAcknowledge = true;
            consumer.ResponseTimeout = TimeSpan.FromSeconds(10);
            consumer.MessageReceived += (_, msg) =>
            {
                firstDeliveryHeader = msg.FindHeader(HorseHeaders.DELIVERY);
            };
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("redeliver-q", true, CancellationToken.None);
            await Task.Delay(300);

            HorseClient producer = await ConnectRaw(ctx.Port);
            await producer.Queue.Push("redeliver-q", new MemoryStream(System.Text.Encoding.UTF8.GetBytes("first-msg")), false, CancellationToken.None);

            await WaitUntil(() => firstDeliveryHeader != "NOT_CHECKED", 5_000);
            await Task.Delay(200);

            // First delivery → DeliveryCount=1 → no Delivery header (only added when > 1)
            Assert.Null(firstDeliveryHeader);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.DisposeAsync();
            try { if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true); }
            catch { }
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  3. Redelivery data persists across queue manager lifecycle
    // ═══════════════════════════════════════════════════════════════════

    #region Redelivery Persistence Integration

    [Fact]
    public async Task Redelivery_AckClearsEntry_FromRedeliveryService()
    {
        var (ctx, dataPath) = await CreatePersistentWithRedelivery();
        try
        {
            await ctx.Rider.Queue.Create("redeliver-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.waitAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.PutBack = PutBackDecision.Regular;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            });

            HorseQueue queue = ctx.Rider.Queue.Find("redeliver-q");
            Assert.NotNull(queue);
            PersistentQueueManager manager = (PersistentQueueManager)queue.Manager;

            int consumeCount = 0;
            HorseClient consumer = new();
            consumer.ResponseTimeout = TimeSpan.FromSeconds(10);
            consumer.MessageReceived += async (client, msg) =>
            {
                int count = Interlocked.Increment(ref consumeCount);
                if (count == 1)
                    await client.SendNegativeAck(msg, CancellationToken.None); // NACK → put-back → redeliver
                else
                    await client.SendAck(msg, CancellationToken.None); // ACK → remove from redelivery
            };
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("redeliver-q", true, CancellationToken.None);
            await Task.Delay(300);

            HorseClient producer = await ConnectRaw(ctx.Port);
            await producer.Queue.Push("redeliver-q", new MemoryStream(System.Text.Encoding.UTF8.GetBytes("ack-clear-test")), false, CancellationToken.None);

            await WaitUntil(() => consumeCount >= 2, 10_000);
            await Task.Delay(1000);

            // After ACK, the redelivery entry should be cleaned up
            var deliveries = manager.RedeliveryService.GetDeliveries();
            Assert.Empty(deliveries);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.DisposeAsync();
            try { if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Redelivery_MultipleMessages_EachTrackedIndependently()
    {
        var (ctx, dataPath) = await CreatePersistentWithRedelivery();
        try
        {
            await ctx.Rider.Queue.Create("redeliver-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.waitAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.PutBack = PutBackDecision.Regular;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            });

            // msg-1: NACK once, ACK on 2nd delivery (count=2)
            // msg-2: ACK immediately (count=1)
            // msg-3: NACK twice, ACK on 3rd delivery (count=3)
            ConcurrentDictionary<string, int> deliveryCounts = new();
            int totalConsumed = 0;

            HorseClient consumer = new();
            consumer.ResponseTimeout = TimeSpan.FromSeconds(10);
            consumer.MessageReceived += async (client, msg) =>
            {
                string content = msg.GetStringContent();
                int currentCount = deliveryCounts.AddOrUpdate(content, 1, (_, v) => v + 1);

                bool shouldAck = content switch
                {
                    "msg-1" => currentCount >= 2,
                    "msg-2" => true,
                    "msg-3" => currentCount >= 3,
                    _ => true
                };

                if (shouldAck)
                {
                    await client.SendAck(msg, CancellationToken.None);
                    Interlocked.Increment(ref totalConsumed);
                }
                else
                {
                    await client.SendNegativeAck(msg, CancellationToken.None);
                }
            };
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("redeliver-q", true, CancellationToken.None);
            await Task.Delay(300);

            HorseClient producer = await ConnectRaw(ctx.Port);
            await producer.Queue.Push("redeliver-q", new MemoryStream(System.Text.Encoding.UTF8.GetBytes("msg-1")), false, CancellationToken.None);
            await producer.Queue.Push("redeliver-q", new MemoryStream(System.Text.Encoding.UTF8.GetBytes("msg-2")), false, CancellationToken.None);
            await producer.Queue.Push("redeliver-q", new MemoryStream(System.Text.Encoding.UTF8.GetBytes("msg-3")), false, CancellationToken.None);

            await WaitUntil(() => totalConsumed >= 3, 20_000);
            await Task.Delay(500);

            Assert.Equal(3, totalConsumed);

            // msg-1: delivered 2 times
            Assert.True(deliveryCounts["msg-1"] >= 2);
            // msg-2: delivered 1 time
            Assert.Equal(1, deliveryCounts["msg-2"]);
            // msg-3: delivered 3 times
            Assert.True(deliveryCounts["msg-3"] >= 3);

            // All entries cleared after ACK
            HorseQueue queue = ctx.Rider.Queue.Find("redeliver-q");
            PersistentQueueManager manager = (PersistentQueueManager)queue.Manager;
            var remaining = manager.RedeliveryService.GetDeliveries();
            Assert.Empty(remaining);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.DisposeAsync();
            try { if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true); }
            catch { }
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  4. Redelivery count visible via server-side queue inspection
    // ═══════════════════════════════════════════════════════════════════

    #region Server-Side Inspection

    [Fact]
    public async Task Redelivery_ServerSide_QueueMessageDeliveryCountUpdated()
    {
        var (ctx, dataPath) = await CreatePersistentWithRedelivery();
        try
        {
            await ctx.Rider.Queue.Create("redeliver-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.waitAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.PutBack = PutBackDecision.Regular;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            });

            HorseQueue queue = ctx.Rider.Queue.Find("redeliver-q");
            Assert.NotNull(queue);

            // Push a message via server API
            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "redeliver-q");
            msg.SetMessageId("test-msg-id");
            msg.SetStringContent("server-side-test");
            await queue.Push(msg);

            // Before any delivery, DeliveryCount should be 0
            QueueMessage queueMsg = queue.Manager.MessageStore.ReadFirst();
            Assert.NotNull(queueMsg);
            Assert.Equal(0, queueMsg.DeliveryCount);

            // Simulate a delivery via BeginSend
            await queue.Manager.DeliveryHandler.BeginSend(queue, queueMsg);
            Assert.Equal(1, queueMsg.DeliveryCount);

            // Check redelivery service
            PersistentQueueManager manager = (PersistentQueueManager)queue.Manager;
            var deliveries = manager.RedeliveryService.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal("test-msg-id", deliveries[0].Key);
            Assert.Equal(1, deliveries[0].Value);

            // Second delivery
            await queue.Manager.DeliveryHandler.BeginSend(queue, queueMsg);
            Assert.Equal(2, queueMsg.DeliveryCount);

            deliveries = manager.RedeliveryService.GetDeliveries();
            Assert.Single(deliveries);
            Assert.Equal(2, deliveries[0].Value);

            // Verify Delivery header is set on 2nd+ delivery
            string header = queueMsg.Message.FindHeader(HorseHeaders.DELIVERY);
            Assert.NotNull(header);
            Assert.Equal("2", header);
        }
        finally
        {
            await ctx.DisposeAsync();
            try { if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true); }
            catch { }
        }
    }

    [Fact]
    public async Task Redelivery_MessageRemoved_EntryCleanedFromService()
    {
        var (ctx, dataPath) = await CreatePersistentWithRedelivery();
        try
        {
            await ctx.Rider.Queue.Create("redeliver-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.waitAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.PutBack = PutBackDecision.Regular;
            });

            HorseQueue queue = ctx.Rider.Queue.Find("redeliver-q");
            PersistentQueueManager manager = (PersistentQueueManager)queue.Manager;

            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "redeliver-q");
            msg.SetMessageId("remove-test-id");
            msg.SetStringContent("remove-test");
            await queue.Push(msg);

            QueueMessage queueMsg = queue.Manager.MessageStore.ReadFirst();

            // Simulate 2 deliveries
            await queue.Manager.DeliveryHandler.BeginSend(queue, queueMsg);
            await queue.Manager.DeliveryHandler.BeginSend(queue, queueMsg);

            Assert.Single(manager.RedeliveryService.GetDeliveries());

            // Remove message (simulates ACK path)
            await queue.Manager.RemoveMessage(queueMsg);

            // Redelivery entry should be gone
            Assert.Empty(manager.RedeliveryService.GetDeliveries());
        }
        finally
        {
            await ctx.DisposeAsync();
            try { if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true); }
            catch { }
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  5. Without redelivery (UseRedelivery=false), no tracking
    // ═══════════════════════════════════════════════════════════════════

    #region No Redelivery

    [Fact]
    public async Task NoRedelivery_DeliveryCountNotTracked_NoDeliveryHeader()
    {
        string dataPath = $"qt-noredel-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.RoundRobin;
                q.Options.Acknowledge = QueueAckDecision.waitAcknowledge;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.AutoQueueCreation = true;
                q.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
                q.Options.PutBack = PutBackDecision.Regular;

                // UseRedelivery = false (default)
                q.UsePersistentQueues(
                    db => db.UseInstantFlush().SetAutoShrink(false),
                    queue =>
                    {
                        queue.Options.CommitWhen = q.Options.CommitWhen;
                        queue.Options.Acknowledge = q.Options.Acknowledge;
                    });
            })
            .Build();

        var (port, server) = await StartServer(rider);
        try
        {
            await rider.Queue.Create("no-redel-q", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.waitAcknowledge;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.PutBack = PutBackDecision.Regular;
                o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            });

            HorseQueue queue = rider.Queue.Find("no-redel-q");
            PersistentQueueManager manager = (PersistentQueueManager)queue.Manager;

            Assert.False(manager.UseRedelivery);
            Assert.Null(manager.RedeliveryService);

            // Push and simulate delivery
            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "no-redel-q");
            msg.SetMessageId("no-redel-msg");
            msg.SetStringContent("test");
            await queue.Push(msg);

            QueueMessage queueMsg = queue.Manager.MessageStore.ReadFirst();
            await queue.Manager.DeliveryHandler.BeginSend(queue, queueMsg);

            // DeliveryCount is not incremented when UseRedelivery=false
            Assert.Equal(0, queueMsg.DeliveryCount);

            // No Delivery header
            string header = queueMsg.Message.FindHeader(HorseHeaders.DELIVERY);
            Assert.Null(header);
        }
        finally
        {
            try { await server?.StopAsync(); } catch { }
            try { if (Directory.Exists(dataPath)) Directory.Delete(dataPath, true); } catch { }
        }
    }

    #endregion
}

