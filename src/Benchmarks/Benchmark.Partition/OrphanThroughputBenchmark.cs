using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Benchmark 4 — Orphan Partition Throughput (label-less distribution)
///
/// When EnableOrphanPartition = true and no label is specified on messages,
/// all messages are routed to the orphan and pushed to all subscribers.
/// Measures how fast the orphan path handles label-less messages.
///
/// Parameters
///   ConsumerCount : number of label-less workers (each gets its own partition + subscribes orphan)
///   MessageCount  : total messages per iteration
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class OrphanThroughputBenchmark : BenchmarkBase
{
    [Params(1, 3, 8)]
    public int ConsumerCount { get; set; }

    [Params(5_000, 20_000)]
    public int MessageCount { get; set; }

    private HorseClient[] _consumers = [];
    private HorseClient   _producer  = null!;
    private const string  Queue      = "bmark-orphan";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        CreatePartitionedQueue(Queue, QueueType.Push,
            maxPartitions: ConsumerCount, subscribersPerPart: 1, enableOrphan: true)
            .GetAwaiter().GetResult();

        _consumers = new HorseClient[ConsumerCount];
        for (int i = 0; i < ConsumerCount; i++)
        {
            var c = ConnectAsync($"orphan-c{i}").GetAwaiter().GetResult();
            c.Queue.SubscribePartitioned(Queue, partitionLabel: null, verifyResponse: true,
                maxPartitions: ConsumerCount, subscribersPerPartition: 1)
             .GetAwaiter().GetResult();
            _consumers[i] = c;
        }

        _producer = ConnectAsync("producer").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        foreach (var c in _consumers) c.Disconnect();
        StopServer();
    }

    [Benchmark(Description = "OrphanLabelLess_Push")]
    public async Task Run()
    {
        for (int i = 0; i < MessageCount; i++)
            await _producer.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false);
    }
}

