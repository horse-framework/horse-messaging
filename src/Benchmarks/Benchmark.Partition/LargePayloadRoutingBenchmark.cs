using System;
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
/// Benchmark 12 — Large Payload Routing
///
/// Validates that partition routing cost stays proportional to payload size
/// and does not introduce extra copies or hidden O(n) scans on large messages.
///
/// Parameters
///   PayloadKB      : message body size in kilobytes
///   PartitionCount : label partitions active during the run
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class LargePayloadRoutingBenchmark : BenchmarkBase
{
    [Params(1, 16, 64, 256)]
    public int PayloadKB { get; set; }

    [Params(1, 10)]
    public int PartitionCount { get; set; }

    private const int MessageCount = 500;

    private byte[]        _payload  = [];
    private HorseClient[] _consumers = [];
    private HorseClient   _producer  = null!;
    private string[]      _labels    = [];
    private const string  Queue      = "bmark-large";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        CreatePartitionedQueue(Queue, QueueType.Push,
            maxPartitions: PartitionCount, subscribersPerPart: 1)
            .GetAwaiter().GetResult();

        _labels    = new string[PartitionCount];
        _consumers = new HorseClient[PartitionCount];

        for (int i = 0; i < PartitionCount; i++)
        {
            _labels[i]    = $"large-p{i}";
            _consumers[i] = ConnectAsync($"large-c{i}").GetAwaiter().GetResult();
            _consumers[i].Queue.SubscribePartitioned(Queue, _labels[i], true, CancellationToken.None)
                          .GetAwaiter().GetResult();
        }

        _producer = ConnectAsync("large-prod").GetAwaiter().GetResult();
    }

    // IterationSetup rebuilds the payload for each (PayloadKB) parameter combination
    [IterationSetup]
    public void BuildPayload()
    {
        _payload = new byte[PayloadKB * 1024];
        new Random(42).NextBytes(_payload);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        foreach (var c in _consumers) c.Disconnect();
        StopServer();
    }

    [Benchmark(Description = "LargePayload_Labeled_Push")]
    public async Task Run()
    {
        int perPart = MessageCount / PartitionCount;
        var tasks   = new Task[PartitionCount];

        for (int p = 0; p < PartitionCount; p++)
        {
            string label   = _labels[p];
            byte[] payload = _payload;
            var    headers = new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label)
            };
            tasks[p] = Task.Run(async () =>
            {
                for (int j = 0; j < perPart; j++)
                    await _producer.Queue.Push(Queue, new System.IO.MemoryStream(payload, writable: false),
                        waitForCommit: false, headers, CancellationToken.None);
            });
        }

        await Task.WhenAll(tasks);
    }
}

