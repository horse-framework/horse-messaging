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
/// Benchmark 8 — Message Routing Cost (PartitionManager.RouteMessage latency)
///
/// Focuses on the routing layer only: producer pushes a message and the benchmark
/// measures total time per Push() call including the PartitionManager label-lookup
/// and the target-queue enqueue. Consumer backpressure is removed by using
/// QueueAckDecision.None with no subscribers (store-only mode) — pure routing cost.
///
/// Parameters
///   PartitionCount : size of the _labelIndex the router must search
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class RoutingCostBenchmark : BenchmarkBase
{
    [Params(1, 10, 50, 100)]
    public int PartitionCount { get; set; }

    private HorseClient   _producer = null!;
    private string[]      _labels   = [];
    private const string  Queue     = "bmark-routing";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        CreatePartitionedQueue(Queue, QueueType.Push,
            maxPartitions: PartitionCount, subscribersPerPart: 1)
            .GetAwaiter().GetResult();

        _labels = new string[PartitionCount];

        // Subscribe consumers to pre-create all partition queues
        for (int i = 0; i < PartitionCount; i++)
        {
            _labels[i] = $"rt-{i}";
            var c = ConnectAsync($"rt-c{i}").GetAwaiter().GetResult();
            c.Queue.SubscribePartitioned(Queue, _labels[i], verifyResponse: true)
             .GetAwaiter().GetResult();
            // We intentionally do NOT store consumers — they stay connected (keeps partitions alive)
            // but we never disconnect them here.  GlobalCleanup stops the server.
        }

        _producer = ConnectAsync("rt-producer").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        StopServer();
    }

    [Benchmark(Description = "Routing_LabelLookup_Push")]
    public async Task LabeledRoute()
    {
        // Round-robin over all labels to stress all label-index slots
        for (int i = 0; i < PartitionCount; i++)
        {
            var headers = new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, _labels[i])
            };
            await _producer.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false, headers);
        }
    }

    [Benchmark(Baseline = true, Description = "Routing_NoLabel_RoundRobin")]
    public async Task LabellessRoute()
    {
        // Push without label — hits the round-robin path inside PartitionManager
        for (int i = 0; i < PartitionCount; i++)
            await _producer.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false);
    }
}

