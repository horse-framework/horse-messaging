using System;
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
/// Benchmark 5 — Partition Scaling (message throughput vs. partition count)
///
/// Keeps total message count constant while increasing the number of partitions
/// (each with its own producer sending to its label). Reveals whether the system
/// scales linearly, sub-linearly, or hits a ceiling.
///
/// 1 partition  : all messages to one label  (single partition baseline)
/// 5 partitions : 5 labels, messages split evenly
/// 10 partitions: 10 labels, …
/// 20 partitions: stress test the PartitionManager
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class PartitionScalingBenchmark : BenchmarkBase
{
    [Params(1, 5, 10, 20)]
    public int PartitionCount { get; set; }

    // Total messages per iteration is fixed so each extra partition means fewer messages/partition
    private const int TotalMessages = 10_000;

    private HorseClient[] _consumers  = [];
    private HorseClient[] _producers  = [];
    private string[]      _labels     = [];
    private const string  Queue       = "bmark-scale";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        CreatePartitionedQueue(Queue, QueueType.Push,
            maxPartitions: PartitionCount, subscribersPerPart: 1, enableOrphan: false)
            .GetAwaiter().GetResult();

        _labels    = new string[PartitionCount];
        _consumers = new HorseClient[PartitionCount];
        _producers = new HorseClient[PartitionCount];

        for (int i = 0; i < PartitionCount; i++)
        {
            _labels[i] = $"scale-{i}";

            var consumer = ConnectAsync($"sc-c{i}").GetAwaiter().GetResult();
            consumer.Queue.SubscribePartitioned(Queue, _labels[i], verifyResponse: true)
                    .GetAwaiter().GetResult();
            _consumers[i] = consumer;

            _producers[i] = ConnectAsync($"sc-p{i}").GetAwaiter().GetResult();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var c in _consumers) c.Disconnect();
        foreach (var p in _producers) p.Disconnect();
        StopServer();
    }

    [Benchmark(Description = "PartitionScale_LabeledPush")]
    public async Task Run()
    {
        int perPartition = TotalMessages / PartitionCount;
        var tasks        = new Task[PartitionCount];

        for (int i = 0; i < PartitionCount; i++)
        {
            int    idx     = i;
            string label   = _labels[idx];
            var    prod    = _producers[idx];
            var    headers = new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label)
            };

            tasks[idx] = Task.Run(async () =>
            {
                for (int j = 0; j < perPartition; j++)
                    await prod.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false, headers);
            });
        }

        await Task.WhenAll(tasks);
    }
}

