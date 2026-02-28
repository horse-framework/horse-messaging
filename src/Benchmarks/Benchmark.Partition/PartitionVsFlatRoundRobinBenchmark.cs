using System.Collections.Generic;
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
/// Benchmark 2 — Partition vs Flat RoundRobin Throughput Comparison
///
/// The classic alternative to partitioning is a single RoundRobin queue with N workers.
/// This benchmark puts the two head-to-head under identical load so you can read
/// the cost (or gain) of partitioning directly from the summary table.
///
/// Parameters
///   WorkerCount   : simulates N workers subscribing
///   MessageCount  : total messages pushed per iteration
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class PartitionVsFlatRoundRobinBenchmark : BenchmarkBase
{
    [Params(2, 5, 10)]
    public int WorkerCount { get; set; }

    [Params(5_000, 20_000)]
    public int MessageCount { get; set; }

    private HorseClient[] _flatWorkers = [];
    private HorseClient[] _partWorkers = [];
    private HorseClient   _producer    = null!;

    private const string FlatQueue = "bmark-flat-rr";
    private const string PartQueue = "bmark-part-rr";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.RoundRobin, QueueAckDecision.None);

        // Flat RoundRobin queue — all workers on one physical queue
        Rider.Queue.Create(FlatQueue, o => o.Type = QueueType.RoundRobin)
             .GetAwaiter().GetResult();

        // Partitioned queue — each worker gets its own partition
        CreatePartitionedQueue(PartQueue, QueueType.RoundRobin,
            maxPartitions: WorkerCount, subscribersPerPart: 1, enableOrphan: false)
            .GetAwaiter().GetResult();

        _flatWorkers = new HorseClient[WorkerCount];
        _partWorkers = new HorseClient[WorkerCount];

        for (int i = 0; i < WorkerCount; i++)
        {
            var fw = ConnectAsync($"flat-w{i}").GetAwaiter().GetResult();
            fw.Queue.Subscribe(FlatQueue, verifyResponse: true).GetAwaiter().GetResult();
            _flatWorkers[i] = fw;

            var pw = ConnectAsync($"part-w{i}").GetAwaiter().GetResult();
            pw.Queue.SubscribePartitioned(PartQueue, partitionLabel: null, verifyResponse: true,
                maxPartitions: WorkerCount, subscribersPerPartition: 1)
              .GetAwaiter().GetResult();
            _partWorkers[i] = pw;
        }

        _producer = ConnectAsync("producer").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        foreach (var c in _flatWorkers) c.Disconnect();
        foreach (var c in _partWorkers) c.Disconnect();
        StopServer();
    }

    [Benchmark(Baseline = true, Description = "FlatRoundRobin")]
    public async Task FlatRoundRobin()
    {
        for (int i = 0; i < MessageCount; i++)
            await _producer.Queue.Push(FlatQueue, NewPayloadStream(), waitForCommit: false);
    }

    [Benchmark(Description = "PartitionedRoundRobin")]
    public async Task PartitionedRoundRobin()
    {
        for (int i = 0; i < MessageCount; i++)
            await _producer.Queue.Push(PartQueue, NewPayloadStream(), waitForCommit: false);
    }
}

