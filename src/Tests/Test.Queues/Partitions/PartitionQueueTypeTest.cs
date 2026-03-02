using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Common;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for all QueueType × Partition combinations:
///   - Push   + Partition  (broadcast within partition)
///   - RoundRobin + Partition  (round-robin within partition)
///   - Pull   + Partition  (pull-on-demand, message stored until requested)
///
/// Also covers:
///   - SubscribersPerPartition = 1  vs > 1
///   - Push vs RoundRobin equivalence when SubscribersPerPartition = 1
///   - Tenant isolation under WaitForAcknowledge
/// </summary>
public class PartitionQueueTypeTest
{
    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static async Task<(HorseRider rider, int port, HorseQueue queue)> CreateServer(
        string queueName,
        QueueType type,
        int maxPartitions = 10,
        int subscribersPerPartition = 1,
        QueueAckDecision ack = QueueAckDecision.None)
    {
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create(queueName, opts =>
        {
            opts.Type = QueueType.Push; // global default; will be overridden below
            opts.Acknowledge = ack;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
            opts.Type = type; // set AFTER Partition so CloneFrom carries it
        });

        HorseQueue queue = rider.Queue.Find(queueName);
        return (rider, port, queue);
    }

    // Fix: ref cannot be used inside lambda — use int[] instead
    private static HorseClient MakeWorker(int[] counter)
    {
        var c = new HorseClient { AutoAcknowledge = true };
        c.MessageReceived += (_, _) => Interlocked.Increment(ref counter[0]);
        return c;
    }

    // ─────────────────────────────────────────────────────────────────
    // Push + Partition: SubscribersPerPartition = 1
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Push + Partition, SubscribersPerPartition=1:
    /// Her label için tek worker vardır; mesaj sadece o worker'a gider.
    /// </summary>
    [Fact]
    public async Task Push_Partition_OneSubscriberPerPartition_MessageGoesToLabeledWorker()
    {
        var (rider, port, queue) = await CreateServer("pp1-q", QueueType.Push, subscribersPerPartition: 1);

        int[] w1 = { 0 }, w2 = { 0 };
        HorseClient worker1 = MakeWorker(w1);
        HorseClient worker2 = MakeWorker(w2);

        await worker1.ConnectAsync("horse://localhost:" + port);
        await worker2.ConnectAsync("horse://localhost:" + port);

        await worker1.Queue.SubscribePartitioned("pp1-q", "w1", true);
        await worker2.Queue.SubscribePartitioned("pp1-q", "w2", true);
        await Task.Delay(400);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        // 4 mesajı w1 label'ı ile gönder
        for (int i = 0; i < 4; i++)
            await producer.Queue.Push("pp1-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });

        await Task.Delay(800);

        Assert.Equal(4, w1[0]);
        Assert.Equal(0, w2[0]);
    }

    /// <summary>
    /// Push + Partition, SubscribersPerPartition=1:
    /// İki ayrı label'a eşit mesaj gönderilince her worker kendi mesajlarını alır.
    /// </summary>
    [Fact]
    public async Task Push_Partition_TwoLabels_EachWorkerReceivesOnlyOwnMessages()
    {
        var (rider, port, queue) = await CreateServer("pp2-q", QueueType.Push, subscribersPerPartition: 1);

        int[] w1 = { 0 }, w2 = { 0 };
        HorseClient worker1 = MakeWorker(w1);
        HorseClient worker2 = MakeWorker(w2);

        await worker1.ConnectAsync("horse://localhost:" + port);
        await worker2.ConnectAsync("horse://localhost:" + port);

        await worker1.Queue.SubscribePartitioned("pp2-q", "labelA", true);
        await worker2.Queue.SubscribePartitioned("pp2-q", "labelB", true);
        await Task.Delay(400);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("pp2-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "labelA") });
        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("pp2-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "labelB") });

        await Task.Delay(800);

        Assert.Equal(3, w1[0]);
        Assert.Equal(5, w2[0]);
    }

    // ─────────────────────────────────────────────────────────────────
    // Push + Partition: SubscribersPerPartition > 1  (broadcast)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Push + Partition, SubscribersPerPartition=2:
    /// Aynı partition'da 2 worker olunca mesaj ikisine de gönderilir (broadcast).
    /// </summary>
    [Fact]
    public async Task Push_Partition_TwoSubscribersPerPartition_BroadcastsToAll()
    {
        var (rider, port, queue) = await CreateServer("pb-q", QueueType.Push, subscribersPerPartition: 2);

        int[] wa = { 0 }, wb = { 0 };
        HorseClient workerA = MakeWorker(wa);
        HorseClient workerB = MakeWorker(wb);

        await workerA.ConnectAsync("horse://localhost:" + port);
        await workerB.ConnectAsync("horse://localhost:" + port);

        // Her ikisi de aynı label → aynı partition
        await workerA.Queue.SubscribePartitioned("pb-q", "shared", true);
        await workerB.Queue.SubscribePartitioned("pb-q", "shared", true);
        await Task.Delay(400);

        // Partition'da 2 subscriber olmalı
        var part = queue.PartitionManager.Partitions.First();
        Assert.Equal(2, part.Queue.ClientsCount());

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        await producer.Queue.Push("pb-q", Encoding.UTF8.GetBytes("hello"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "shared") });

        await Task.Delay(600);

        Assert.Equal(1, wa[0]);
        Assert.Equal(1, wb[0]);
    }

    // ─────────────────────────────────────────────────────────────────
    // RoundRobin + Partition: SubscribersPerPartition = 1
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// RoundRobin + Partition, SubscribersPerPartition=1:
    /// Push ile davranış aynıdır — tek subscriber olunca fark yok.
    /// </summary>
    [Fact]
    public async Task RoundRobin_Partition_OneSubscriberPerPartition_SameBehaviorAsPush()
    {
        var (rider, port, queue) = await CreateServer("rr1-q", QueueType.RoundRobin, subscribersPerPartition: 1);

        int[] w1 = { 0 }, w2 = { 0 };
        HorseClient worker1 = MakeWorker(w1);
        HorseClient worker2 = MakeWorker(w2);

        await worker1.ConnectAsync("horse://localhost:" + port);
        await worker2.ConnectAsync("horse://localhost:" + port);

        await worker1.Queue.SubscribePartitioned("rr1-q", "t1", true);
        await worker2.Queue.SubscribePartitioned("rr1-q", "t2", true);
        await Task.Delay(400);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 4; i++)
            await producer.Queue.Push("rr1-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "t1") });

        await Task.Delay(800);

        Assert.Equal(4, w1[0]);
        Assert.Equal(0, w2[0]);
    }

    // ─────────────────────────────────────────────────────────────────
    // RoundRobin + Partition: SubscribersPerPartition > 1
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// RoundRobin + Partition, SubscribersPerPartition=2:
    /// Aynı partition'da 2 worker varken mesajlar sırayla dağıtılır (1'e, broadcast değil).
    /// </summary>
    [Fact]
    public async Task RoundRobin_Partition_TwoSubscribersPerPartition_DistributesOneAtATime()
    {
        var (rider, port, queue) = await CreateServer("rr2-q", QueueType.RoundRobin, subscribersPerPartition: 2);

        int[] wa = { 0 }, wb = { 0 };
        HorseClient workerA = MakeWorker(wa);
        HorseClient workerB = MakeWorker(wb);

        await workerA.ConnectAsync("horse://localhost:" + port);
        await workerB.ConnectAsync("horse://localhost:" + port);

        // Her ikisi de aynı label → aynı partition
        await workerA.Queue.SubscribePartitioned("rr2-q", "shared", true);
        await workerB.Queue.SubscribePartitioned("rr2-q", "shared", true);
        await Task.Delay(400);

        var part = queue.PartitionManager.Partitions.First();
        Assert.Equal(2, part.Queue.ClientsCount());

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 4; i++)
            await producer.Queue.Push("rr2-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "shared") });

        await Task.Delay(800);

        Assert.Equal(4, wa[0] + wb[0]);
        Assert.True(wa[0] >= 1, $"workerA almadı, wa={wa[0]}");
        Assert.True(wb[0] >= 1, $"workerB almadı, wb={wb[0]}");
    }

    // ─────────────────────────────────────────────────────────────────
    // RoundRobin vs Push: SubscribersPerPartition > 1 farkı
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// RoundRobin queue'da WaitForAcknowledge ile meşgul worker atlanır,
    /// mesaj başka worker'a gider. Partition'da ise mesaj aynı partition'da bekler.
    /// </summary>
    [Fact]
    public async Task Partition_vs_RoundRobinQueue_MessageOwnership_UnderWaitForAck()
    {
        // ── Classic RoundRobin queue (partition yok) ──────────────────
        var (riderRR, portRR, _) = await PartitionTestServer.Create();
        await riderRR.Queue.Create("cmp-rr-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
        });

        int[] rrW1 = { 0 }, rrW2 = { 0 };
        HorseClient rrWorker1 = new HorseClient { AutoAcknowledge = false }; // meşgul
        HorseClient rrWorker2 = new HorseClient { AutoAcknowledge = true };
        rrWorker1.MessageReceived += (_, _) => Interlocked.Increment(ref rrW1[0]);
        rrWorker2.MessageReceived += (_, _) => Interlocked.Increment(ref rrW2[0]);
        await rrWorker1.ConnectAsync("horse://localhost:" + portRR);
        await rrWorker2.ConnectAsync("horse://localhost:" + portRR);
        await rrWorker1.Queue.Subscribe("cmp-rr-q", true);
        await rrWorker2.Queue.Subscribe("cmp-rr-q", true);
        await Task.Delay(300);
        HorseClient rrProducer = new HorseClient();
        await rrProducer.ConnectAsync("horse://localhost:" + portRR);
        for (int i = 0; i < 4; i++)
            await rrProducer.Queue.Push("cmp-rr-q", Encoding.UTF8.GetBytes("msg"), false);
        await Task.Delay(1000);

        // RoundRobin: worker1 meşgul → atlanır, mesajlar worker2'ye de gider
        Assert.True(rrW2[0] >= 1, $"RoundRobin: worker2 en az 1 mesaj almalı, rrW2={rrW2[0]}");

        // ── Partition queue ───────────────────────────────────────────
        var (riderPT, portPT, _) = await PartitionTestServer.Create();
        await riderPT.Queue.Create("cmp-pt-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        int[] ptW1 = { 0 }, ptW2 = { 0 };
        HorseClient ptWorker1 = new HorseClient { AutoAcknowledge = false }; // meşgul
        HorseClient ptWorker2 = new HorseClient { AutoAcknowledge = true };
        ptWorker1.MessageReceived += (_, _) => Interlocked.Increment(ref ptW1[0]);
        ptWorker2.MessageReceived += (_, _) => Interlocked.Increment(ref ptW2[0]);
        await ptWorker1.ConnectAsync("horse://localhost:" + portPT);
        await ptWorker2.ConnectAsync("horse://localhost:" + portPT);
        await ptWorker1.Queue.SubscribePartitioned("cmp-pt-q", "w1-label", true);
        await ptWorker2.Queue.SubscribePartitioned("cmp-pt-q", "w2-label", true);
        await Task.Delay(300);
        HorseClient ptProducer = new HorseClient();
        await ptProducer.ConnectAsync("horse://localhost:" + portPT);
        for (int i = 0; i < 4; i++)
            await ptProducer.Queue.Push("cmp-pt-q", Encoding.UTF8.GetBytes("msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1-label") });
        await Task.Delay(1000);

        // Partition: mesajlar w1'e ait partition'da bekler, w2 hiç almadı
        Assert.Equal(0, ptW2[0]);
        Assert.True(ptW1[0] >= 1, $"Partition worker1 en az 1 mesaj almış olmalı, ptW1={ptW1[0]}");
    }

    // ─────────────────────────────────────────────────────────────────
    // Pull + Partition (server-side assertion — no client Pull API needed)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pull + Partition: mesaj partition deposuna yazılır, consumer pull etmeden gelmez.
    /// Server-side MessageStore.Count() ile doğrulanır.
    /// </summary>
    [Fact]
    public async Task Pull_Partition_MessageStoredUntilPulled()
    {
        var (rider, port, queue) = await CreateServer("pull1-q", QueueType.Pull, subscribersPerPartition: 1);

        int[] received = { 0 };
        HorseClient worker = new HorseClient { AutoAcknowledge = true };
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
        await worker.ConnectAsync("horse://localhost:" + port);
        await worker.Queue.SubscribePartitioned("pull1-q", "pw1", true);
        await Task.Delay(400);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        await producer.Queue.Push("pull1-q", Encoding.UTF8.GetBytes("stored"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "pw1") });

        await Task.Delay(400);

        // Mesaj pull edilmeden önce partition deposunda durmalı
        var part = queue.PartitionManager.Partitions.First();
        Assert.Equal(1, part.Queue.Manager.MessageStore.Count());

        // Pull isteği gönder
        PullContainer container = await worker.Queue.Pull(
            new PullRequest { Queue = part.Queue.Name, Count = 1 });

        await Task.Delay(400);

        Assert.Equal(1, container.ReceivedCount);
    }

    [Fact]
    public async Task Pull_Partition_MessageNotDeliveredAutomatically()
    {
        var (rider, port, queue) = await CreateServer("pull2-q", QueueType.Pull, subscribersPerPartition: 1);

        int[] received = { 0 };
        HorseClient worker = new HorseClient { AutoAcknowledge = true };
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
        await worker.ConnectAsync("horse://localhost:" + port);
        await worker.Queue.SubscribePartitioned("pull2-q", "pw2", true);
        await Task.Delay(400);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        await producer.Queue.Push("pull2-q", Encoding.UTF8.GetBytes("stored"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "pw2") });

        // Pull etmeden 800ms bekle — otomatik iletilmemeli
        await Task.Delay(800);

        Assert.Equal(0, received[0]);

        var part = queue.PartitionManager.Partitions.First();
        Assert.Equal(1, part.Queue.Manager.MessageStore.Count());
    }

    [Fact]
    public async Task Pull_Partition_IsolationMaintained_EachWorkerPullsOwnPartition()
    {
        var (rider, port, queue) = await CreateServer("pull3-q", QueueType.Pull, subscribersPerPartition: 1);

        HorseClient worker1 = new HorseClient { AutoAcknowledge = true };
        HorseClient worker2 = new HorseClient { AutoAcknowledge = true };
        await worker1.ConnectAsync("horse://localhost:" + port);
        await worker2.ConnectAsync("horse://localhost:" + port);
        await worker1.Queue.SubscribePartitioned("pull3-q", "p1", true);
        await worker2.Queue.SubscribePartitioned("pull3-q", "p2", true);
        await Task.Delay(400);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 2; i++)
            await producer.Queue.Push("pull3-q", Encoding.UTF8.GetBytes("p1-msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "p1") });
        await producer.Queue.Push("pull3-q", Encoding.UTF8.GetBytes("p2-msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "p2") });
        await Task.Delay(300);

        var part1 = queue.PartitionManager.Partitions.First(p => p.Label == "p1");
        var part2 = queue.PartitionManager.Partitions.First(p => p.Label == "p2");

        Assert.Equal(2, part1.Queue.Manager.MessageStore.Count());
        Assert.Equal(1, part2.Queue.Manager.MessageStore.Count());
    }

    // ─────────────────────────────────────────────────────────────────
    // QueueType inheritance
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task QueueType_IsInheritedByPartitionQueues()
    {
        var (riderRR, portRR, queueRR) = await CreateServer("inh-rr-q", QueueType.RoundRobin);
        var (riderPL, portPL, queuePL) = await CreateServer("inh-pl-q", QueueType.Pull);

        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + portRR);
        await c1.Queue.SubscribePartitioned("inh-rr-q", "lbl", true);
        await Task.Delay(300);

        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync("horse://localhost:" + portPL);
        await c2.Queue.SubscribePartitioned("inh-pl-q", "lbl", true);
        await Task.Delay(300);

        var rrPart = queueRR.PartitionManager.Partitions.First();
        var plPart = queuePL.PartitionManager.Partitions.First();

        Assert.Equal(QueueType.RoundRobin, rrPart.Queue.Type);
        Assert.Equal(QueueType.Pull, plPart.Queue.Type);
    }

    /// <summary>
    /// Push ve RoundRobin, SubscribersPerPartition=1 olduğunda aynı mesaj sayısını alır.
    /// </summary>
    [Fact]
    public async Task Push_And_RoundRobin_WithOneSubscriber_BehaveIdentically()
    {
        int[] totalPush = { 0 }, totalRR = { 0 };

        // Push
        {
            var (rider, port, _) = await CreateServer("eq-push-q", QueueType.Push, subscribersPerPartition: 1);
            HorseClient w = MakeWorker(totalPush);
            await w.ConnectAsync("horse://localhost:" + port);
            await w.Queue.SubscribePartitioned("eq-push-q", "lbl", true);
            await Task.Delay(300);
            HorseClient p = new HorseClient();
            await p.ConnectAsync("horse://localhost:" + port);
            for (int i = 0; i < 5; i++)
                await p.Queue.Push("eq-push-q", Encoding.UTF8.GetBytes("m"), false,
                    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") });
            await Task.Delay(600);
        }

        // RoundRobin
        {
            var (rider, port, _) = await CreateServer("eq-rr-q", QueueType.RoundRobin, subscribersPerPartition: 1);
            HorseClient w = MakeWorker(totalRR);
            await w.ConnectAsync("horse://localhost:" + port);
            await w.Queue.SubscribePartitioned("eq-rr-q", "lbl", true);
            await Task.Delay(300);
            HorseClient p = new HorseClient();
            await p.ConnectAsync("horse://localhost:" + port);
            for (int i = 0; i < 5; i++)
                await p.Queue.Push("eq-rr-q", Encoding.UTF8.GetBytes("m"), false,
                    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") });
            await Task.Delay(600);
        }

        Assert.Equal(5, totalPush[0]);
        Assert.Equal(5, totalRR[0]);
        Assert.Equal(totalPush[0], totalRR[0]);
    }

    // ─────────────────────────────────────────────────────────────────
    // Tenant isolation under WaitForAcknowledge
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tenant izolasyonu: tenant-A'nın yavaş worker'ı tenant-B'yi etkilememeli.
    /// WaitForAcknowledge ile tenant-A'nın partition beklerken tenant-B devam eder.
    /// </summary>
    [Fact]
    public async Task TenantIsolation_SlowTenantA_DoesNotBlockTenantB()
    {
        var (rider, port, _) = await PartitionTestServer.Create();
        await rider.Queue.Create("iso-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        // tenant-A: ACK göndermez (yavaş simülasyonu)
        int[] aCount = { 0 };
        HorseClient workerA = new HorseClient { AutoAcknowledge = false };
        workerA.MessageReceived += (_, _) => Interlocked.Increment(ref aCount[0]);

        // tenant-B: hemen ACK gönderir
        int[] bCount = { 0 };
        HorseClient workerB = new HorseClient { AutoAcknowledge = true };
        workerB.MessageReceived += (_, _) => Interlocked.Increment(ref bCount[0]);

        await workerA.ConnectAsync("horse://localhost:" + port);
        await workerB.ConnectAsync("horse://localhost:" + port);
        await workerA.Queue.SubscribePartitioned("iso-q", "tenantA", true);
        await workerB.Queue.SubscribePartitioned("iso-q", "tenantB", true);
        await Task.Delay(400);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 3; i++)
        {
            await producer.Queue.Push("iso-q", Encoding.UTF8.GetBytes("a-msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenantA") });
            await producer.Queue.Push("iso-q", Encoding.UTF8.GetBytes("b-msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenantB") });
        }

        await Task.Delay(1500);

        // tenant-B tüm mesajlarını aldı (A'nın yavaşlığından bağımsız)
        Assert.Equal(3, bCount[0]);
        Assert.True(aCount[0] >= 1, $"tenant-A en az 1 mesaj almış olmalı, aCount={aCount[0]}");
    }
}

