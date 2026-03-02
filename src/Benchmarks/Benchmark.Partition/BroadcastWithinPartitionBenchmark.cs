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
/// Benchmark 9 — SubscribersPerPartition > 1 (broadcast within partition)
///
/// When QueueType = Push and SubscribersPerPartition > 1, every message arriving
/// at a partition is broadcast to ALL subscribers of that partition.
/// Measures throughput and allocation cost for different fan-out counts.
///
/// Parameters
///   FanOut : number of subscribers per partition (= broadcast width)
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class BroadcastWithinPartitionBenchmark : BenchmarkBase
{
    [Params(1, 2, 5)]
    public int FanOut { get; set; }

    private const int MessageCount = 2_000;
    private const int Partitions   = 3;   // fixed, we vary FanOut

    private HorseClient[] _consumers = [];
    private HorseClient   _producer  = null!;
    private string[]      _labels    = [];
    private const string  Queue      = "bmark-broadcast";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        CreatePartitionedQueue(Queue, QueueType.Push,
            maxPartitions: Partitions, subscribersPerPart: FanOut)
            .GetAwaiter().GetResult();

        _labels    = new string[Partitions];
        _consumers = new HorseClient[Partitions * FanOut];

        for (int p = 0; p < Partitions; p++)
        {
            _labels[p] = $"broad-p{p}";
            for (int f = 0; f < FanOut; f++)
            {
                var c = ConnectAsync($"broad-c{p}-{f}").GetAwaiter().GetResult();
                c.Queue.SubscribePartitioned(Queue, _labels[p], verifyResponse: true,
                    maxPartitions: Partitions, subscribersPerPartition: FanOut)
                 .GetAwaiter().GetResult();
                _consumers[p * FanOut + f] = c;
            }
        }

        _producer = ConnectAsync("broad-producer").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        foreach (var c in _consumers) c.Disconnect();
        StopServer();
    }

    [Benchmark(Description = "Broadcast_Push_PerPartition")]
    public async Task Run()
    {
        int perPartition = MessageCount / Partitions;
        var tasks        = new Task[Partitions];

        for (int p = 0; p < Partitions; p++)
        {
            string label   = _labels[p];
            var    headers = new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label)
            };
            tasks[p] = Task.Run(async () =>
            {
                for (int j = 0; j < perPartition; j++)
                    await _producer.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false, headers);
            });
        }

        await Task.WhenAll(tasks);
    }
}

