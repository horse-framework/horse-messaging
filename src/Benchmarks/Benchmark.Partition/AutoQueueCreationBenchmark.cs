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
/// Benchmark 11 — AutoQueueCreation (SubscribePartitioned without pre-created queue)
///
/// Measures the overhead of the AutoQueueCreation path where the first subscribe
/// request creates the queue and its partitioned structure on the fly.
/// Each iteration creates a uniquely-named queue so GlobalSetup is minimal.
///
/// Parameters
///   PartitionCount : partitions to create via subscribe in one batch
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class AutoQueueCreationBenchmark : BenchmarkBase
{
    [Params(1, 5, 10)]
    public int PartitionCount { get; set; }

    private int _iteration;

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        Rider.Queue.Options.AutoQueueCreation = true;
        _iteration = 0;
    }

    [GlobalCleanup]
    public void Cleanup() => StopServer();

    [Benchmark(Description = "AutoCreate_SubscribePartitioned")]
    public async Task Run()
    {
        int iter    = Interlocked.Increment(ref _iteration);
        string queueName = $"auto-q-{iter}";

        var consumers = new HorseClient[PartitionCount];

        for (int i = 0; i < PartitionCount; i++)
        {
            consumers[i] = await ConnectAsync($"auto-c{i}");
            await consumers[i].Queue.SubscribePartitioned(
                queueName,
                partitionLabel:         $"auto-t{i}",
                verifyResponse:         true,
                maxPartitions:          PartitionCount,
                subscribersPerPartition: 1);
        }

        // push 5 messages to verify the queue is live
        var producer = await ConnectAsync("auto-prod");
        var headers  = new[]
        {
            new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"auto-t0")
        };
        for (int j = 0; j < 5; j++)
            await producer.Queue.Push(queueName, NewPayloadStream(), waitForCommit: false, headers);

        producer.Disconnect();
        foreach (var c in consumers) c.Disconnect();
    }
}

