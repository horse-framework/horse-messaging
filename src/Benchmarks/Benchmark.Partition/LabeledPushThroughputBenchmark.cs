using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;

namespace Benchmark.Partition;

/// <summary>
/// Benchmark 1 — Labeled Push Throughput
///
/// Measures messages/sec delivered to a dedicated partition when a producer
/// sends with PARTITION_LABEL and a single consumer is subscribed with that label.
///
/// Parameters
///   PartitionCount  : number of label partitions (= number of tenants / workers)
///   MessageCount    : total messages sent in each iteration
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class LabeledPushThroughputBenchmark : BenchmarkBase
{
    [Params(1, 4, 10)]
    public int PartitionCount { get; set; }

    [Params(1_000, 10_000)]
    public int MessageCount { get; set; }

    private HorseClient[] _consumers = [];
    private HorseClient   _producer  = null!;
    private string[]      _labels    = [];
    private const string  Queue      = "bmark-labeled";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        CreatePartitionedQueue(Queue, QueueType.Push, maxPartitions: PartitionCount, subscribersPerPart: 1)
            .GetAwaiter().GetResult();

        _labels    = new string[PartitionCount];
        _consumers = new HorseClient[PartitionCount];

        for (int i = 0; i < PartitionCount; i++)
        {
            _labels[i] = $"tenant-{i}";
            var c = ConnectAsync($"consumer-{i}").GetAwaiter().GetResult();
            c.Queue.SubscribePartitioned(Queue, _labels[i], true, CancellationToken.None)
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

    [Benchmark(Description = "LabeledPush_Throughput")]
    public async Task Run()
    {
        int perLabel = MessageCount / PartitionCount;
        var tasks    = new Task[PartitionCount];

        for (int i = 0; i < PartitionCount; i++)
        {
            string label = _labels[i];
            tasks[i] = Task.Run(async () =>
            {
                var headers = new[]
                {
                    new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label)
                };
                for (int j = 0; j < perLabel; j++)
                    await _producer.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false, headers, CancellationToken.None);
            });
        }

        await Task.WhenAll(tasks);
    }
}

