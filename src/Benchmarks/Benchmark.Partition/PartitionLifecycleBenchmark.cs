using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;

namespace Benchmark.Partition;

/// <summary>
/// Benchmark 6 — Partition Create/Destroy Overhead
///
/// Measures the cost of dynamically creating and destroying partitions
/// at runtime. Each iteration creates N partition subscriptions
/// (which causes N partition queues to be created), sends a batch of messages,
/// then disconnects all consumers (triggering AutoDestroy = NoConsumers).
///
/// This is the "cold path" — the overhead you pay once per new tenant.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class PartitionLifecycleBenchmark : BenchmarkBase
{
    [Params(1, 5, 10)]
    public int PartitionsToCreate { get; set; }

    private HorseClient _producer = null!;
    private const string Queue = "bmark-lifecycle";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        // Queue is created fresh for each benchmark run;
        // it is NOT recreated per iteration — only consumers come and go.
        CreatePartitionedQueue(Queue, QueueType.Push,
            maxPartitions: PartitionsToCreate + 5,
            subscribersPerPart: 1,
            enableOrphan: false,
            autoDestroy: PartitionAutoDestroy.NoConsumers)
            .GetAwaiter().GetResult();

        _producer = ConnectAsync("producer").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        StopServer();
    }

    /// <summary>
    /// Per iteration: connect N consumers (partition creation), push 10 messages each,
    /// disconnect consumers (partition destruction).
    /// </summary>
    [Benchmark(Description = "Partition_Create_Push_Destroy")]
    public async Task Run()
    {
        var consumers = new HorseClient[PartitionsToCreate];
        var labels    = new string[PartitionsToCreate];

        // ── connect (creates partitions) ────────────────────────────────────
        for (int i = 0; i < PartitionsToCreate; i++)
        {
            labels[i]    = $"lc-{Guid.NewGuid():N}";   // unique label per iteration
            consumers[i] = await ConnectAsync($"lc-c{i}");
            await consumers[i].Queue.SubscribePartitioned(
                Queue, labels[i], verifyResponse: true,
                maxPartitions: PartitionsToCreate + 5, subscribersPerPartition: 1);
        }

        // ── push 10 messages to each partition ──────────────────────────────
        var tasks = new Task[PartitionsToCreate];
        for (int i = 0; i < PartitionsToCreate; i++)
        {
            string label   = labels[i];
            var    headers = new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label)
            };
            tasks[i] = Task.Run(async () =>
            {
                for (int j = 0; j < 10; j++)
                    await _producer.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false, headers);
            });
        }
        await Task.WhenAll(tasks);

        // ── disconnect (triggers AutoDestroy = NoConsumers) ─────────────────
        for (int i = 0; i < PartitionsToCreate; i++)
            consumers[i].Disconnect();

        // Give the AutoDestroy timer a moment to fire (timer interval = 5 s).
        // We don't wait the full 5 s here — lifecycle cost benchmark measures
        // create+push latency, not the async destroy.
    }
}

