using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Reproduces the exact parrot scenario:
///   1. API pushes a trigger message to upstream queue
///   2. Upstream consumer processes it and publishes N events to downstream queue IN A LOOP
///   3. Downstream consumer should receive ALL N events
///
/// Both consumers run in the same process (same HorseClient), matching parrot's
/// messaging client architecture where orderFetcher has both FetchOrders and MapOrders handlers.
/// </summary>
public class ConcurrentPublishBugTest
{
    /// <summary>
    /// Same-process upstream + downstream consumer with WaitForAcknowledge.
    /// Upstream handler publishes 10 messages sequentially in a loop.
    /// Downstream should receive all 10.
    ///
    /// This is the EXACT parrot scenario:
    ///   - FetchOrders consumer fetches N orders
    ///   - For each order, publishes MapOrdersEvent
    ///   - MapOrders consumer should process all N events
    /// </summary>
    [Fact]
    public async Task SameProcess_UpstreamPublishesInLoop_DownstreamReceivesAll_Sequential()
    {
        await using var ctx = await PartitionTestServer.Create("memory", o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        // Upstream queue (like FetchOrdersEvent)
        await ctx.Rider.Queue.Create("up-seq-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        // Downstream queue (like MapOrdersEvent)
        await ctx.Rider.Queue.Create("down-seq-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        const int eventCount = 10;
        int downstreamReceived = 0;
        int upstreamProcessed = 0;
        ManualResetEventSlim upstreamDone = new(false);

        // SAME HorseClient for both upstream and downstream — like parrot's messaging client
        HorseClient workerClient = new HorseClient { AutoAcknowledge = false };

        // Downstream handler: process message, send ACK with 200ms delay (simulate DB work)
        workerClient.MessageReceived += (client, message) =>
        {
            string target = message.Target;
            var horseClient = (HorseClient)client;

            if (target.Contains("down-seq-q"))
            {
                // Downstream consumer
                Interlocked.Increment(ref downstreamReceived);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(200); // simulate processing
                    await horseClient.SendAck(message, CancellationToken.None);
                });
            }
            else if (target.Contains("up-seq-q"))
            {
                // Upstream consumer: publishes N events to downstream queue IN A LOOP
                _ = Task.Run(async () =>
                {
                    try
                    {
                        for (int i = 0; i < eventCount; i++)
                        {
                            await horseClient.Queue.Push("down-seq-q",
                                Encoding.UTF8.GetBytes($"event-{i}"), false,
                                new[]
                                {
                                    new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl")
                                },
                                CancellationToken.None);
                        }

                        Interlocked.Increment(ref upstreamProcessed);

                        // Send ACK for upstream message AFTER all publishes complete
                        await horseClient.SendAck(message, CancellationToken.None);
                    }
                    finally
                    {
                        upstreamDone.Set();
                    }
                });
            }
        };

        await workerClient.ConnectAsync($"horse://localhost:{ctx.Port}");
        // Subscribe to BOTH queues on the same client (like parrot's orderFetcher)
        await workerClient.Queue.SubscribePartitioned("up-seq-q", "lbl", true, CancellationToken.None);
        await workerClient.Queue.SubscribePartitioned("down-seq-q", "lbl", true, CancellationToken.None);
        await Task.Delay(500);

        // API trigger: push one message to upstream queue
        HorseClient apiClient = new HorseClient();
        await apiClient.ConnectAsync($"horse://localhost:{ctx.Port}");
        await apiClient.Queue.Push("up-seq-q", Encoding.UTF8.GetBytes("trigger"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") },
            CancellationToken.None);

        // Wait for upstream handler to complete
        Assert.True(upstreamDone.Wait(TimeSpan.FromSeconds(10)), "Upstream handler did not complete");
        Assert.Equal(1, upstreamProcessed);

        // Wait for all downstream messages (generous but bounded — 15s max)
        for (int i = 0; i < 150 && downstreamReceived < eventCount; i++)
            await Task.Delay(100);

        Assert.Equal(eventCount, downstreamReceived);

        apiClient.Disconnect();
        workerClient.Disconnect();
    }

    /// <summary>
    /// Same scenario but upstream handler uses Task.WhenAll (parallel publish).
    /// Matches parrot's SaveOrders, CheckInvoiceStatuses, TrackDelivery patterns.
    /// </summary>
    [Fact]
    public async Task SameProcess_UpstreamPublishesInParallel_DownstreamReceivesAll()
    {
        await using var ctx = await PartitionTestServer.Create("memory", o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        await ctx.Rider.Queue.Create("up-par-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        await ctx.Rider.Queue.Create("down-par-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        const int eventCount = 10;
        int downstreamReceived = 0;
        ManualResetEventSlim upstreamDone = new(false);

        HorseClient workerClient = new HorseClient { AutoAcknowledge = false };
        workerClient.MessageReceived += (client, message) =>
        {
            string target = message.Target;
            var horseClient = (HorseClient)client;

            if (target.Contains("down-par-q"))
            {
                Interlocked.Increment(ref downstreamReceived);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(200);
                    await horseClient.SendAck(message, CancellationToken.None);
                });
            }
            else if (target.Contains("up-par-q"))
            {
                // Upstream: publish all events in PARALLEL (Task.WhenAll)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var tasks = Enumerable.Range(0, eventCount).Select(i =>
                            horseClient.Queue.Push("down-par-q",
                                Encoding.UTF8.GetBytes($"event-{i}"), false,
                                new[]
                                {
                                    new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl")
                                },
                                CancellationToken.None)
                        ).ToList();

                        await Task.WhenAll(tasks);
                        await horseClient.SendAck(message, CancellationToken.None);
                    }
                    finally
                    {
                        upstreamDone.Set();
                    }
                });
            }
        };

        await workerClient.ConnectAsync($"horse://localhost:{ctx.Port}");
        await workerClient.Queue.SubscribePartitioned("up-par-q", "lbl", true, CancellationToken.None);
        await workerClient.Queue.SubscribePartitioned("down-par-q", "lbl", true, CancellationToken.None);
        await Task.Delay(500);

        HorseClient apiClient = new HorseClient();
        await apiClient.ConnectAsync($"horse://localhost:{ctx.Port}");
        await apiClient.Queue.Push("up-par-q", Encoding.UTF8.GetBytes("trigger"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") },
            CancellationToken.None);

        Assert.True(upstreamDone.Wait(TimeSpan.FromSeconds(10)), "Upstream handler did not complete");

        for (int i = 0; i < 150 && downstreamReceived < eventCount; i++)
            await Task.Delay(100);

        Assert.Equal(eventCount, downstreamReceived);

        apiClient.Disconnect();
        workerClient.Disconnect();
    }

    /// <summary>
    /// Multiple triggers in rapid succession: API sends 3 upstream messages.
    /// Each upstream consumer publishes 5 downstream events.
    /// Total expected downstream: 15 messages.
    ///
    /// This tests the scenario where multiple upstream messages are processed
    /// back-to-back, each publishing a batch of downstream events.
    /// </summary>
    [Fact]
    public async Task SameProcess_MultipleTriggers_AllDownstreamEventsConsumed()
    {
        await using var ctx = await PartitionTestServer.Create("memory", o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        await ctx.Rider.Queue.Create("up-multi-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        await ctx.Rider.Queue.Create("down-multi-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        const int triggersCount = 3;
        const int eventsPerTrigger = 5;
        const int totalExpected = triggersCount * eventsPerTrigger;

        int downstreamReceived = 0;
        int upstreamProcessed = 0;

        HorseClient workerClient = new HorseClient { AutoAcknowledge = false };
        workerClient.MessageReceived += (client, message) =>
        {
            string target = message.Target;
            var horseClient = (HorseClient)client;

            if (target.Contains("down-multi-q"))
            {
                Interlocked.Increment(ref downstreamReceived);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await horseClient.SendAck(message, CancellationToken.None);
                });
            }
            else if (target.Contains("up-multi-q"))
            {
                _ = Task.Run(async () =>
                {
                    for (int i = 0; i < eventsPerTrigger; i++)
                    {
                        await horseClient.Queue.Push("down-multi-q",
                            Encoding.UTF8.GetBytes($"batch-{upstreamProcessed}-event-{i}"), false,
                            new[]
                            {
                                new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl")
                            },
                            CancellationToken.None);
                    }

                    Interlocked.Increment(ref upstreamProcessed);
                    await horseClient.SendAck(message, CancellationToken.None);
                });
            }
        };

        await workerClient.ConnectAsync($"horse://localhost:{ctx.Port}");
        await workerClient.Queue.SubscribePartitioned("up-multi-q", "lbl", true, CancellationToken.None);
        await workerClient.Queue.SubscribePartitioned("down-multi-q", "lbl", true, CancellationToken.None);
        await Task.Delay(500);

        // API sends 3 trigger messages rapidly
        HorseClient apiClient = new HorseClient();
        await apiClient.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < triggersCount; i++)
            await apiClient.Queue.Push("up-multi-q", Encoding.UTF8.GetBytes($"trigger-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") },
                CancellationToken.None);

        // Wait for all upstream handlers and downstream consumers
        for (int i = 0; i < 200 && (upstreamProcessed < triggersCount || downstreamReceived < totalExpected); i++)
            await Task.Delay(100);

        Assert.Equal(triggersCount, upstreamProcessed);
        Assert.Equal(totalExpected, downstreamReceived);

        apiClient.Disconnect();
        workerClient.Disconnect();
    }
}
