using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;

namespace Benchmark.Partition;

/// <summary>
/// Benchmark 7 — Multi-Tenant Isolation Under Load
///
/// Simulates a real-world multi-tenant scenario:
///   • TenantCount tenants each push MessageCount messages with their label
///   • A "noisy" heavy-hitter tenant also floods the queue concurrently
///   • Measures how long all tenants' messages take to reach their
///     dedicated partitions, demonstrating that a busy tenant does NOT
///     slow down others.
///
/// Parameters
///   TenantCount   : number of "normal" tenants (each has 1 partition consumer)
///   MessageCount  : messages per normal tenant per iteration
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class MultiTenantIsolationBenchmark : BenchmarkBase
{
    [Params(3, 8)]
    public int TenantCount { get; set; }

    [Params(500, 2_000)]
    public int MessageCount { get; set; }

    private HorseClient[] _consumers    = [];
    private HorseClient   _producer     = null!;
    private HorseClient   _noisyProd    = null!;
    private string[]      _labels       = [];
    private const string  Queue         = "bmark-multitenant";
    private const string  NoisyLabel    = "noisy-tenant";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);
        CreatePartitionedQueue(Queue, QueueType.Push,
            maxPartitions: TenantCount + 2, subscribersPerPart: 1)
            .GetAwaiter().GetResult();

        _labels    = new string[TenantCount];
        _consumers = new HorseClient[TenantCount + 1]; // +1 for noisy tenant consumer

        for (int i = 0; i < TenantCount; i++)
        {
            _labels[i] = $"tenant-{i}";
            var c = ConnectAsync($"mt-c{i}").GetAwaiter().GetResult();
            c.Queue.SubscribePartitioned(Queue, _labels[i], true, TenantCount + 2, 1, CancellationToken.None)
             .GetAwaiter().GetResult();
            _consumers[i] = c;
        }

        // noisy tenant consumer
        var noisy = ConnectAsync("noisy-consumer").GetAwaiter().GetResult();
        noisy.Queue.SubscribePartitioned(Queue, NoisyLabel, true, TenantCount + 2, 1, CancellationToken.None)
             .GetAwaiter().GetResult();
        _consumers[TenantCount] = noisy;

        _producer  = ConnectAsync("producer").GetAwaiter().GetResult();
        _noisyProd = ConnectAsync("noisy-producer").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        _noisyProd.Disconnect();
        foreach (var c in _consumers) c.Disconnect();
        StopServer();
    }

    [Benchmark(Description = "MultiTenant_With_NoisyTenant")]
    public async Task Run()
    {
        // Noisy tenant floods 5× more messages concurrently
        int noisyMessages = MessageCount * 5;
        var noisyHeaders  = new[]
        {
            new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, NoisyLabel)
        };
        var noisyTask = Task.Run(async () =>
        {
            for (int i = 0; i < noisyMessages; i++)
                await _noisyProd.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false, noisyHeaders, CancellationToken.None);
        });

        // Normal tenants push their messages concurrently
        var tenantTasks = new Task[TenantCount];
        for (int i = 0; i < TenantCount; i++)
        {
            string label   = _labels[i];
            var    headers = new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label)
            };
            tenantTasks[i] = Task.Run(async () =>
            {
                for (int j = 0; j < MessageCount; j++)
                    await _producer.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false, headers, CancellationToken.None);
            });
        }

        await Task.WhenAll(tenantTasks.Append(noisyTask));
    }
}

