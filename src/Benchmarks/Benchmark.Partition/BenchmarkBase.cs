using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Horse.Server;

namespace Benchmark.Partition;

/// <summary>
/// Base class for all partition benchmarks.
/// Spins up an in-process HorseServer on a random port, tears it down after each benchmark.
/// Each derived class drives one concrete scenario (label routing, orphan, RoundRobin, Pull, …).
/// </summary>
public abstract class BenchmarkBase
{
    // ── infrastructure ──────────────────────────────────────────────────────
    protected HorseRider    Rider  = null!;
    protected HorseServer   Server = null!;
    protected int           Port;

    // small fixed payload — we measure routing/queueing, not serialisation
    protected static readonly byte[] Payload = Encoding.UTF8.GetBytes(new string('x', 128));

    // ── helpers ─────────────────────────────────────────────────────────────

    protected void StartServer(
        QueueType       queueType      = QueueType.Push,
        QueueAckDecision ackDecision   = QueueAckDecision.None,
        bool            memoryQueues   = true)
    {
        Rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o =>
            {
                o.DataPath = $"bmark-data-{Environment.TickCount}-{Random.Shared.Next(0, 99999)}";
            })
            .ConfigureQueues(q =>
            {
                q.Options.Type              = queueType;
                q.Options.Acknowledge       = ackDecision;
                q.Options.AutoQueueCreation = true;
                q.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);

                if (memoryQueues)
                    q.UseMemoryQueues();
            })
            .Build();

        for (int attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                Port = Random.Shared.Next(10000, 60000);
                var opts = HorseServerOptions.CreateDefault();
                opts.Hosts[0].Port = Port;

                Server = new HorseServer(opts);
                Server.UseRider(Rider);
                Server.StartAsync().GetAwaiter().GetResult();
                return;
            }
            catch
            {
                Thread.Sleep(5);
            }
        }

        throw new InvalidOperationException("Could not bind server port after 30 attempts.");
    }

    protected void StopServer()
    {
        try { Server?.StopAsync().GetAwaiter().GetResult(); } catch { /* ignore */ }

        // clean up temp data folders
        try
        {
            if (Rider?.Options?.DataPath is { } path && Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch { /* ignore */ }
    }

    protected static HorseClient BuildClient(string name = "bmark")
    {
        var c = new HorseClient();
        c.SetClientName(name);
        return c;
    }

    protected async Task<HorseClient> ConnectAsync(string? name = null)
    {
        var c = BuildClient(name ?? "bmark");
        await c.ConnectAsync($"horse://localhost:{Port}");
        if (!c.IsConnected) throw new InvalidOperationException("Client could not connect.");
        return c;
    }

    /// <summary>Creates a partitioned queue on the server.</summary>
    protected async Task CreatePartitionedQueue(
        string          queueName,
        QueueType       type                = QueueType.Push,
        int             maxPartitions       = 10,
        int             subscribersPerPart  = 1,
        bool            enableOrphan        = true,
        PartitionAutoDestroy autoDestroy     = PartitionAutoDestroy.Disabled,
        QueueAckDecision ackDecision        = QueueAckDecision.None)
    {
        await Rider.Queue.Create(queueName, o =>
        {
            o.Type      = type;
            o.Acknowledge = ackDecision;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            o.Partition = new PartitionOptions
            {
                Enabled                = true,
                MaxPartitionCount      = maxPartitions,
                SubscribersPerPartition = subscribersPerPart,
                EnableOrphanPartition  = enableOrphan,
                AutoDestroy            = autoDestroy,
                AutoDestroyIdleSeconds = 5
            };
        });
    }

    protected static MemoryStream NewPayloadStream() => new(Payload, writable: false);
}

