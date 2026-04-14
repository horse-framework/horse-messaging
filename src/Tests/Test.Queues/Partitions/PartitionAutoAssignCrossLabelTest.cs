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
using Xunit.Abstractions;

namespace Test.Queues.Partitions;

/// <summary>
/// Regression test for the AutoAssignWorkers cross-label contamination bug.
/// </summary>
public class PartitionAutoAssignCrossLabelTest
{
    private readonly ITestOutputHelper _output;

    public PartitionAutoAssignCrossLabelTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private static async Task RunWithAutoAssignQueue(
        Func<TestHorseRider, int, HorseQueue, Task> action,
        string name = "EventQueue",
        int autoDestroyIdleSeconds = 2)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create(name, opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages,
                    AutoAssignWorkers = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 1,
                    SubscribersPerPartition = 1,
                    AutoDestroyIdleSeconds = autoDestroyIdleSeconds
                };
            });

            HorseQueue queue = server.Rider.Queue.Find(name);
            await action(server, port, queue);
        });
    }

    private static async Task<HorseClient> ConnectOnly(int port)
    {
        HorseClient client = new HorseClient();
        client.AutoSubscribe = false;
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        return client;
    }

    private static async Task SubscribeWithLabel(HorseClient client, string queue, string label)
    {
        HorseResult result = await client.Queue.Subscribe(queue, true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) },
            CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, result.Code);
    }

    private static HashSet<string> GetClientPartitionLabels(HorseQueue parentQueue, string clientId)
    {
        var labels = new HashSet<string>();

        foreach (PartitionEntry entry in parentQueue.PartitionManager.Partitions)
        {
            bool isSubscribed = entry.Queue.Clients.Any(c => c.Client.UniqueId == clientId);
            if (isSubscribed && entry.Label != null)
                labels.Add(entry.Label);
        }

        return labels;
    }

    [Fact]
    public async Task AutoAssign_MessagePush_AfterDestroy_CrossLabel()
    {
        await RunWithAutoAssignQueue(async (_, port, queue) =>
        {
            HorseClient eventFree = await ConnectOnly(port);
            HorseClient eventStandard = await ConnectOnly(port);

            await SubscribeWithLabel(eventFree, "EventQueue", "free");
            await SubscribeWithLabel(eventStandard, "EventQueue", "standard");

            await Task.Delay(500);

            string freeId = eventFree.ClientId;
            string standardId = eventStandard.ClientId;

            Assert.Equal(2, queue.PartitionManager.Partitions.Count());
            Assert.Single(GetClientPartitionLabels(queue, freeId));
            Assert.Contains("free", GetClientPartitionLabels(queue, freeId));
            Assert.Single(GetClientPartitionLabels(queue, standardId));
            Assert.Contains("standard", GetClientPartitionLabels(queue, standardId));

            _output.WriteLine($"Phase 1 OK: free={freeId}, standard={standardId}");

            await Task.Delay(4000);

            Assert.Empty(queue.PartitionManager.Partitions);
            Assert.True(eventFree.IsConnected);
            Assert.True(eventStandard.IsConnected);

            _output.WriteLine("Phase 2 OK: All partitions destroyed, clients still connected");

            HorseMessage pushMsg = new HorseMessage(MessageType.QueueMessage, "EventQueue");
            pushMsg.SetStringContent("test-payload");
            pushMsg.AddHeader(HorseHeaders.PARTITION_LABEL, "standard");
            PushResult pushResult = await queue.Push(pushMsg);

            _output.WriteLine($"Push result: {pushResult}");

            await Task.Delay(1000);

            HashSet<string> eventFreeLabels = GetClientPartitionLabels(queue, freeId);
            HashSet<string> eventStandardLabels = GetClientPartitionLabels(queue, standardId);

            _output.WriteLine($"EVENT_CLIENT_FREE partitions: [{string.Join(", ", eventFreeLabels)}]");
            _output.WriteLine($"EVENT_CLIENT_STANDARD partitions: [{string.Join(", ", eventStandardLabels)}]");

            Assert.DoesNotContain("standard", eventFreeLabels);
        });
    }

    [Fact]
    public async Task AutoAssign_MessagePush_AfterDestroy_MultiLabel_CrossContamination()
    {
        await RunWithAutoAssignQueue(async (_, port, queue) =>
        {
            HorseClient eventFree = await ConnectOnly(port);
            HorseClient eventStandard = await ConnectOnly(port);
            HorseClient eventPremium = await ConnectOnly(port);

            await SubscribeWithLabel(eventFree, "EventQueue", "free");
            await SubscribeWithLabel(eventStandard, "EventQueue", "standard");
            await SubscribeWithLabel(eventPremium, "EventQueue", "premium");

            await Task.Delay(500);

            string freeId = eventFree.ClientId;
            string standardId = eventStandard.ClientId;
            string premiumId = eventPremium.ClientId;

            Assert.Equal(3, queue.PartitionManager.Partitions.Count());

            await Task.Delay(4000);

            Assert.Empty(queue.PartitionManager.Partitions);
            Assert.True(eventFree.IsConnected);
            Assert.True(eventStandard.IsConnected);
            Assert.True(eventPremium.IsConnected);

            HorseMessage msg1 = new HorseMessage(MessageType.QueueMessage, "EventQueue");
            msg1.SetStringContent("msg-standard");
            msg1.AddHeader(HorseHeaders.PARTITION_LABEL, "standard");
            await queue.Push(msg1);
            await Task.Delay(300);

            HorseMessage msg2 = new HorseMessage(MessageType.QueueMessage, "EventQueue");
            msg2.SetStringContent("msg-premium");
            msg2.AddHeader(HorseHeaders.PARTITION_LABEL, "premium");
            await queue.Push(msg2);
            await Task.Delay(300);

            HorseMessage msg3 = new HorseMessage(MessageType.QueueMessage, "EventQueue");
            msg3.SetStringContent("msg-free");
            msg3.AddHeader(HorseHeaders.PARTITION_LABEL, "free");
            await queue.Push(msg3);
            await Task.Delay(500);

            HashSet<string> freeLabels = GetClientPartitionLabels(queue, freeId);
            HashSet<string> standardLabels = GetClientPartitionLabels(queue, standardId);
            HashSet<string> premiumLabels = GetClientPartitionLabels(queue, premiumId);

            _output.WriteLine($"EVENT_FREE in: [{string.Join(", ", freeLabels)}]");
            _output.WriteLine($"EVENT_STANDARD in: [{string.Join(", ", standardLabels)}]");
            _output.WriteLine($"EVENT_PREMIUM in: [{string.Join(", ", premiumLabels)}]");

            Assert.DoesNotContain("standard", freeLabels);
            Assert.DoesNotContain("premium", freeLabels);
            Assert.DoesNotContain("free", standardLabels);
            Assert.DoesNotContain("premium", standardLabels);
            Assert.DoesNotContain("free", premiumLabels);
            Assert.DoesNotContain("standard", premiumLabels);
        });
    }

    [Fact]
    public async Task AutoAssign_TenantPartitions_WorkerFloatsSameTier_NoCrossTierLeak()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("OrderCreated-free", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.AutoQueueCreation = true;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages,
                    AutoAssignWorkers = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 0,
                    SubscribersPerPartition = 1,
                    AutoDestroyIdleSeconds = 2
                };
            });

            await server.Rider.Queue.Create("OrderCreated-standard", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.AutoQueueCreation = true;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages,
                    AutoAssignWorkers = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 0,
                    SubscribersPerPartition = 1,
                    AutoDestroyIdleSeconds = 2
                };
            });

            HorseQueue queueFree = server.Rider.Queue.Find("OrderCreated-free");
            HorseQueue queueStandard = server.Rider.Queue.Find("OrderCreated-standard");
            Assert.NotNull(queueFree);
            Assert.NotNull(queueStandard);

            HorseClient workerFree = await ConnectOnly(port);
            HorseClient workerStandard = await ConnectOnly(port);

            HorseResult r1 = await workerFree.Queue.Subscribe("OrderCreated-free", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            HorseResult r2 = await workerStandard.Queue.Subscribe("OrderCreated-standard", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r2.Code);

            await Task.Delay(300);

            string freeId = workerFree.ClientId;
            string standardId = workerStandard.ClientId;

            _output.WriteLine($"WorkerFree={freeId}, WorkerStandard={standardId}");

            HorseMessage msgFreeTenant1 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-free");
            msgFreeTenant1.SetStringContent("free-tenant-1-msg");
            msgFreeTenant1.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-1");
            await queueFree.Push(msgFreeTenant1);
            await Task.Delay(300);

            HorseMessage msgFreeTenant2 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-free");
            msgFreeTenant2.SetStringContent("free-tenant-2-msg");
            msgFreeTenant2.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-2");
            await queueFree.Push(msgFreeTenant2);
            await Task.Delay(300);

            HorseMessage msgStdTenant1 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-standard");
            msgStdTenant1.SetStringContent("std-tenant-1-msg");
            msgStdTenant1.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-1");
            await queueStandard.Push(msgStdTenant1);
            await Task.Delay(300);

            HashSet<string> freeWorkerInFreeQ = GetClientPartitionLabels(queueFree, freeId);
            HashSet<string> freeWorkerInStdQ = GetClientPartitionLabels(queueStandard, freeId);
            HashSet<string> stdWorkerInStdQ = GetClientPartitionLabels(queueStandard, standardId);
            HashSet<string> stdWorkerInFreeQ = GetClientPartitionLabels(queueFree, standardId);

            _output.WriteLine($"WorkerFree in free-queue: [{string.Join(", ", freeWorkerInFreeQ)}]");
            _output.WriteLine($"WorkerFree in std-queue: [{string.Join(", ", freeWorkerInStdQ)}]");
            _output.WriteLine($"WorkerStandard in std-queue: [{string.Join(", ", stdWorkerInStdQ)}]");
            _output.WriteLine($"WorkerStandard in free-queue: [{string.Join(", ", stdWorkerInFreeQ)}]");

            Assert.Contains("tenant-1", freeWorkerInFreeQ);
            Assert.Contains("tenant-2", freeWorkerInFreeQ);
            Assert.Empty(freeWorkerInStdQ);
            Assert.Contains("tenant-1", stdWorkerInStdQ);
            Assert.Empty(stdWorkerInFreeQ);

            _output.WriteLine("Phase 2 OK: Tier isolation verified, multi-tenant within tier OK");

            await Task.Delay(4000);

            Assert.Empty(queueFree.PartitionManager.Partitions);
            Assert.Empty(queueStandard.PartitionManager.Partitions);
            Assert.True(workerFree.IsConnected);
            Assert.True(workerStandard.IsConnected);

            _output.WriteLine("Phase 3: All partitions destroyed, workers still connected");

            HorseMessage msgFreeTenant3 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-free");
            msgFreeTenant3.SetStringContent("free-tenant-3-msg");
            msgFreeTenant3.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-3");
            await queueFree.Push(msgFreeTenant3);
            await Task.Delay(200);

            HashSet<string> freeWorkerInFreeQ2 = GetClientPartitionLabels(queueFree, freeId);
            HashSet<string> freeWorkerInStdQ2 = GetClientPartitionLabels(queueStandard, freeId);

            _output.WriteLine($"After push free - WorkerFree in free-queue: [{string.Join(", ", freeWorkerInFreeQ2)}]");
            _output.WriteLine($"After push free - WorkerFree in std-queue: [{string.Join(", ", freeWorkerInStdQ2)}]");

            Assert.Contains("tenant-3", freeWorkerInFreeQ2);
            Assert.Empty(freeWorkerInStdQ2);

            HorseMessage msgStdTenant3 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-standard");
            msgStdTenant3.SetStringContent("std-tenant-3-msg");
            msgStdTenant3.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-3");
            await queueStandard.Push(msgStdTenant3);
            await Task.Delay(200);

            HashSet<string> stdWorkerInStdQ2 = GetClientPartitionLabels(queueStandard, standardId);
            HashSet<string> stdWorkerInFreeQ2 = GetClientPartitionLabels(queueFree, standardId);

            _output.WriteLine($"After push std - WorkerStandard in std-queue: [{string.Join(", ", stdWorkerInStdQ2)}]");
            _output.WriteLine($"After push std - WorkerStandard in free-queue: [{string.Join(", ", stdWorkerInFreeQ2)}]");

            Assert.Contains("tenant-3", stdWorkerInStdQ2);
            Assert.Empty(stdWorkerInFreeQ2);
        });
    }

    [Fact]
    public async Task AutoAssign_CombinedTierAndTenant_LabelIsolation()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("FetchOrdersEvent", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages,
                    AutoAssignWorkers = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 1,
                    SubscribersPerPartition = 1,
                    AutoDestroyIdleSeconds = 2
                };
            });

            await server.Rider.Queue.Create("CompareProducts-free", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.AutoQueueCreation = true;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages,
                    AutoAssignWorkers = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 0,
                    SubscribersPerPartition = 1,
                    AutoDestroyIdleSeconds = 2
                };
            });

            await server.Rider.Queue.Create("CompareProducts-standard", opts =>
            {
                opts.Type = QueueType.RoundRobin;
                opts.Acknowledge = QueueAckDecision.None;
                opts.AutoQueueCreation = true;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages,
                    AutoAssignWorkers = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 0,
                    SubscribersPerPartition = 1,
                    AutoDestroyIdleSeconds = 2
                };
            });

            HorseQueue fetchQueue = server.Rider.Queue.Find("FetchOrdersEvent");
            HorseQueue compareFreeQueue = server.Rider.Queue.Find("CompareProducts-free");
            HorseQueue compareStdQueue = server.Rider.Queue.Find("CompareProducts-standard");

            HorseClient workerFree = await ConnectOnly(port);
            HorseClient workerStandard = await ConnectOnly(port);

            string freeId = workerFree.ClientId;
            string standardId = workerStandard.ClientId;

            await SubscribeWithLabel(workerFree, "FetchOrdersEvent", "free");
            await SubscribeWithLabel(workerStandard, "FetchOrdersEvent", "standard");

            HorseResult r1 = await workerFree.Queue.Subscribe("CompareProducts-free", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r1.Code);

            HorseResult r2 = await workerStandard.Queue.Subscribe("CompareProducts-standard", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, r2.Code);

            await Task.Delay(500);

            Assert.Equal(2, fetchQueue.PartitionManager.Partitions.Count());
            Assert.Contains("free", GetClientPartitionLabels(fetchQueue, freeId));
            Assert.Contains("standard", GetClientPartitionLabels(fetchQueue, standardId));

            _output.WriteLine("Initial subscribe OK");

            HorseMessage tenantMsg1 = new HorseMessage(MessageType.QueueMessage, "CompareProducts-free");
            tenantMsg1.SetStringContent("compare-free-t1");
            tenantMsg1.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-1");
            await compareFreeQueue.Push(tenantMsg1);
            await Task.Delay(300);

            HorseMessage tenantMsg2 = new HorseMessage(MessageType.QueueMessage, "CompareProducts-standard");
            tenantMsg2.SetStringContent("compare-std-t1");
            tenantMsg2.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-1");
            await compareStdQueue.Push(tenantMsg2);
            await Task.Delay(300);

            Assert.Contains("tenant-1", GetClientPartitionLabels(compareFreeQueue, freeId));
            Assert.Empty(GetClientPartitionLabels(compareStdQueue, freeId));
            Assert.Contains("tenant-1", GetClientPartitionLabels(compareStdQueue, standardId));
            Assert.Empty(GetClientPartitionLabels(compareFreeQueue, standardId));

            _output.WriteLine("Tenant partition assignment OK");

            await Task.Delay(4000);

            Assert.Empty(fetchQueue.PartitionManager.Partitions);
            Assert.True(workerFree.IsConnected);
            Assert.True(workerStandard.IsConnected);

            _output.WriteLine("FetchOrdersEvent partitions destroyed");

            HorseMessage eventMsgStd = new HorseMessage(MessageType.QueueMessage, "FetchOrdersEvent");
            eventMsgStd.SetStringContent("fetch-std-payload");
            eventMsgStd.AddHeader(HorseHeaders.PARTITION_LABEL, "standard");
            await fetchQueue.Push(eventMsgStd);
            await Task.Delay(500);

            HorseMessage eventMsgFree = new HorseMessage(MessageType.QueueMessage, "FetchOrdersEvent");
            eventMsgFree.SetStringContent("fetch-free-payload");
            eventMsgFree.AddHeader(HorseHeaders.PARTITION_LABEL, "free");
            await fetchQueue.Push(eventMsgFree);
            await Task.Delay(500);

            HashSet<string> freeInFetch = GetClientPartitionLabels(fetchQueue, freeId);
            HashSet<string> stdInFetch = GetClientPartitionLabels(fetchQueue, standardId);

            _output.WriteLine($"After re-create — WorkerFree in FetchOrdersEvent: [{string.Join(", ", freeInFetch)}]");
            _output.WriteLine($"After re-create — WorkerStandard in FetchOrdersEvent: [{string.Join(", ", stdInFetch)}]");

            Assert.DoesNotContain("standard", freeInFetch);
            Assert.DoesNotContain("free", stdInFetch);
        });
    }
}
