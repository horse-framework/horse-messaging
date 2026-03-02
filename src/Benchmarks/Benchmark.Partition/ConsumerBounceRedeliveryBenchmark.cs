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
/// Benchmark 10 — Consumer-Offline Buffering and Re-delivery
///
/// Tests the "store-and-forward" path:
///   1. Consumer subscribes to a labeled partition.
///   2. Consumer disconnects.
///   3. Producer pushes MessageCount messages to that label
///      (messages are buffered in the partition store).
///   4. Consumer reconnects.
///   5. Benchmark measures total time until all messages have been re-delivered.
///
/// Parameters
///   MessageCount : messages buffered while consumer is offline
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ConsumerBounceRedeliveryBenchmark : BenchmarkBase
{
    [Params(100, 1_000, 5_000)]
    public int MessageCount { get; set; }

    private HorseClient _producer = null!;
    private const string Queue     = "bmark-bounce";
    private const string Label     = "bounce-tenant";

    [GlobalSetup]
    public void Setup()
    {
        StartServer(QueueType.Push, QueueAckDecision.None);

        // Offline messages stay in labeled partition only
        CreatePartitionedQueue(Queue, QueueType.Push,
            maxPartitions: 5, subscribersPerPart: 1)
            .GetAwaiter().GetResult();

        // Create partition by subscribing once
        var initConsumer = ConnectAsync("bounce-init").GetAwaiter().GetResult();
        initConsumer.Queue.SubscribePartitioned(Queue, Label, verifyResponse: true)
                    .GetAwaiter().GetResult();
        initConsumer.Disconnect();

        _producer = ConnectAsync("bounce-producer").GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _producer.Disconnect();
        StopServer();
    }

    [Benchmark(Description = "Bounce_Buffer_Redeliver")]
    public async Task Run()
    {
        // ── Phase 1: buffer MessageCount messages (no consumer) ─────────────
        var headers = new[]
        {
            new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, Label)
        };
        for (int i = 0; i < MessageCount; i++)
            await _producer.Queue.Push(Queue, NewPayloadStream(), waitForCommit: false, headers);

        // ── Phase 2: consumer reconnects and drains ──────────────────────────
        int received = 0;
        var tcs      = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int expected = MessageCount;

        var consumer = await ConnectAsync("bounce-consumer");
        consumer.MessageReceived += (c, msg) =>
        {
            if (msg.Type == MessageType.QueueMessage)
                if (Interlocked.Increment(ref received) >= expected)
                    tcs.TrySetResult(true);
        };

        await consumer.Queue.SubscribePartitioned(Queue, Label, verifyResponse: true);

        // Wait for all messages or timeout (30 s)
        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        consumer.Disconnect();
    }
}

