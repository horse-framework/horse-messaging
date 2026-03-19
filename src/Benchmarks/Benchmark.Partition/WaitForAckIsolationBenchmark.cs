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
/// Benchmark 3 — WaitForAcknowledge Isolation
///
/// With WaitForAcknowledge mode:
///   • On a single flat queue all workers are blocked while any one is processing.
///   • On partitioned queues each partition is blocked independently; other partitions continue.
///
/// This benchmark measures effective end-to-end latency (time to push N messages
/// and receive all acks back) for both configurations.
/// A simulated slow consumer (Thread.Sleep) exaggerates the difference.
///
/// Parameters
///   WorkerCount  : number of parallel consumer connections
///   MessageCount : total messages per iteration
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class WaitForAckIsolationBenchmark : BenchmarkBase
{
    [Params(2, 4)]
    public int WorkerCount { get; set; }

    [Params(200, 1_000)]
    public int MessageCount { get; set; }

    private HorseClient[] _flatConsumers = [];
    private HorseClient[] _partConsumers = [];
    private HorseClient   _producer      = null!;

    private const string FlatQueue = "bmark-ack-flat";
    private const string PartQueue = "bmark-ack-part";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.WaitForAcknowledge);

        Rider.Queue.Create(FlatQueue, o =>
        {
            o.Type      = QueueType.Push;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = System.TimeSpan.FromSeconds(30);
        }).GetAwaiter().GetResult();

        CreatePartitionedQueue(PartQueue, QueueType.Push,
            maxPartitions: WorkerCount, subscribersPerPart: 1,
            ackDecision: QueueAckDecision.WaitForAcknowledge)
            .GetAwaiter().GetResult();

        _flatConsumers = new HorseClient[WorkerCount];
        _partConsumers = new HorseClient[WorkerCount];

        for (int i = 0; i < WorkerCount; i++)
        {
            // Flat consumers: subscribe and immediately auto-ack
            var fc = ConnectAsync($"flat-c{i}").GetAwaiter().GetResult();
            fc.Queue.Subscribe(FlatQueue, true, CancellationToken.None).GetAwaiter().GetResult();
            fc.AutoAcknowledge = true;
            _flatConsumers[i] = fc;

            // Partitioned consumers: each gets own label, immediately auto-ack
            string label = $"worker-{i}";
            var pc = ConnectAsync($"part-c{i}").GetAwaiter().GetResult();
            pc.Queue.SubscribePartitioned(PartQueue, label, true, WorkerCount, 1, CancellationToken.None)
              .GetAwaiter().GetResult();
            pc.AutoAcknowledge = true;
            _partConsumers[i] = pc;
        }

        _producer = ConnectAsync("producer").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        foreach (var c in _flatConsumers) c.Disconnect();
        foreach (var c in _partConsumers) c.Disconnect();
        StopServer();
    }

    [Benchmark(Baseline = true, Description = "WaitForAck_FlatQueue")]
    public async Task FlatQueueAck()
    {
        int sent = 0;
        while (sent < MessageCount)
        {
            await _producer.Queue.Push(FlatQueue, NewPayloadStream(), waitForCommit: false, CancellationToken.None);
            sent++;
        }
    }

    [Benchmark(Description = "WaitForAck_Partitioned")]
    public async Task PartitionedQueueAck()
    {
        int perPart = MessageCount / WorkerCount;
        var tasks   = new Task[WorkerCount];

        for (int i = 0; i < WorkerCount; i++)
        {
            string label = $"worker-{i}";
            int    count = perPart;
            tasks[i] = Task.Run(async () =>
            {
                var headers = new[]
                {
                    new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label)
                };
                for (int j = 0; j < count; j++)
                    await _producer.Queue.Push(PartQueue, NewPayloadStream(), waitForCommit: false, headers, CancellationToken.None);
            });
        }

        await Task.WhenAll(tasks);
    }
}

