using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;
using Xunit;
using Xunit.Abstractions;

namespace Test.Server.Benchmarks;

/// <summary>
/// Benchmark tests measuring CPU usage, RAM usage and throughput
/// for queue push operations from the server's perspective.
/// </summary>
public class QueuePushBenchmarkTest : ITestOutputSink
{
    private readonly ITestOutputHelper _output;

    // --- Thresholds ---
    private const int SMALL_BATCH = 10_000;
    private const int LARGE_BATCH = 50_000;
    private const int MAX_ELAPSED_SECONDS = 30;
    private const int MIN_THROUGHPUT = 2_000; // msg/sec
    private const long MAX_RAM_DELTA_MB = 500;
    private const int MAX_CPU_TIME_SECONDS = 60;

    // Small payload (~100 bytes)
    private static readonly byte[] MessagePayload = Encoding.UTF8.GetBytes(new string('x', 100));

    public QueuePushBenchmarkTest(ITestOutputHelper output)
    {
        _output = output;
    }

    void ITestOutputSink.WriteLine(string message) => _output.WriteLine(message);

    #region Helpers

    private static (HorseRider rider, HorseServer server, int port) StartPushServer(
        QueueAckDecision ackDecision = QueueAckDecision.None,
        TimeSpan? ackTimeout = null)
    {
        var rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o =>
            {
                var rnd = new Random();
                o.DataPath = $"bench-data-{Environment.TickCount}-{rnd.Next(0, 100000)}";
            })
            .ConfigureQueues(cfg =>
            {
                cfg.Options.Type = QueueType.Push;
                cfg.Options.AutoQueueCreation = true;
                cfg.Options.Acknowledge = ackDecision;
                cfg.Options.AcknowledgeTimeout = ackTimeout ?? TimeSpan.FromSeconds(30);
                cfg.Options.CommitWhen = ackDecision == QueueAckDecision.WaitForAcknowledge
                    ? CommitWhen.AfterAcknowledge
                    : CommitWhen.AfterReceived;
                cfg.Options.MessageTimeout = new MessageTimeoutStrategy
                {
                    MessageDuration = 300,
                    Policy = MessageTimeoutPolicy.Delete
                };
                cfg.UseMemoryQueues();
            })
            .ConfigureChannels(c => c.UseCustomPersistentConfigurator(null))
            .Build();

        var rnd2 = new Random();
        HorseServer server = null;
        int port = 0;

        for (int i = 0; i < 50; i++)
        {
            try
            {
                port = rnd2.Next(15000, 64000);
                var opts = HorseServerOptions.CreateDefault();
                opts.Hosts[0].Port = port;
                opts.PingInterval = 300;
                opts.RequestTimeout = 300;

                server = new HorseServer(opts);
                server.UseRider(rider);
                server.StartAsync().GetAwaiter().GetResult();
                break;
            }
            catch
            {
                Thread.Sleep(5);
                server = null;
            }
        }

        if (server == null)
            throw new Exception("Could not start Horse server on any port.");

        return (rider, server, port);
    }

    private static async Task<HorseClient> CreateClient(int port, string name = null)
    {
        var client = new HorseClient();
        if (!string.IsNullOrEmpty(name))
            client.SetClientName(name);

        await client.ConnectAsync($"horse://localhost:{port}");
        return client;
    }

    /// <summary>
    /// Logs CPU/Wall ratio as a diagnostic metric.
    /// NOTE: Process.TotalProcessorTime sums ALL threads in the process (server + consumers + producer).
    /// In multi-threaded in-process tests, ratio > 1.0 is expected on multi-core CPUs.
    /// This metric is NOT reliable for spin-wait detection — use latency-based tests instead.
    /// </summary>
    private void LogCpuWallRatio(BenchmarkSnapshot snapshot, string phase)
    {
        double cpuRatio = snapshot.Elapsed.TotalSeconds > 0
            ? snapshot.CpuTimeDelta.TotalSeconds / snapshot.Elapsed.TotalSeconds
            : 0;

        _output.WriteLine($"[{phase}] CPU/Wall ratio: {cpuRatio:F2} (diagnostic only — multi-thread in-process)");
    }

    #endregion

    /// <summary>
    /// Scenario 1: Messages are pushed without any consumer.
    /// After push completes, a consumer connects and drains all messages.
    /// Measures push throughput, CPU, RAM and CPU/Wall ratio.
    /// </summary>
    [Fact]
    public async Task PushWithoutConsumer_ThenConsumerConnects()
    {
        var (rider, server, port) = StartPushServer();

        try
        {
            const string queueName = "bench-push-no-consumer";
            const int messageCount = SMALL_BATCH; // 10,000

            // ── Phase 1: Push messages without consumer ──
            var producer = await CreateClient(port, "bench-producer");
            Assert.True(producer.IsConnected);

            var metrics = new BenchmarkMetrics();
            metrics.Start();

            for (int i = 0; i < messageCount; i++)
            {
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);
            }

            var pushSnapshot = metrics.Stop();
            pushSnapshot.PrintSummary("PUSH (no consumer)", messageCount, this);

            // Verify messages are queued on server
            await Task.Delay(500);
            HorseQueue queue = rider.Queue.Find(queueName);
            Assert.NotNull(queue);

            int storedCount = queue.Manager.MessageStore.Count();
            _output.WriteLine($"Messages in store after push: {storedCount}");
            Assert.Equal(messageCount, storedCount);
            Assert.Equal(messageCount, queue.Info.ReceivedMessages);

            // Assert push thresholds
            double pushThroughput = BenchmarkMetrics.Throughput(messageCount, pushSnapshot.Elapsed);
            Assert.True(pushSnapshot.Elapsed.TotalSeconds < MAX_ELAPSED_SECONDS,
                $"Push took {pushSnapshot.Elapsed.TotalSeconds:F1}s, max allowed {MAX_ELAPSED_SECONDS}s");
            Assert.True(pushThroughput >= MIN_THROUGHPUT,
                $"Push throughput {pushThroughput:F0} msg/sec below minimum {MIN_THROUGHPUT}");
            Assert.True(pushSnapshot.RamDeltaMB < MAX_RAM_DELTA_MB,
                $"Push RAM delta {pushSnapshot.RamDeltaMB:F1} MB exceeds {MAX_RAM_DELTA_MB} MB");
            Assert.True(pushSnapshot.CpuTimeDelta.TotalSeconds < MAX_CPU_TIME_SECONDS,
                $"Push CPU time {pushSnapshot.CpuTimeDelta.TotalSeconds:F1}s exceeds {MAX_CPU_TIME_SECONDS}s");
            LogCpuWallRatio(pushSnapshot, "PUSH (no consumer)");

            // ── Phase 2: Consumer connects and drains ──
            int receivedCount = 0;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var consumer = await CreateClient(port, "bench-consumer");
            Assert.True(consumer.IsConnected);

            consumer.MessageReceived += (_, _) =>
            {
                int count = Interlocked.Increment(ref receivedCount);
                if (count >= messageCount)
                    allReceived.TrySetResult(true);
            };

            var consumeMetrics = new BenchmarkMetrics();
            consumeMetrics.Start();

            var subResult = await consumer.Queue.Subscribe(queueName, true);
            Assert.Equal(HorseResultCode.Ok, subResult.Code);

            // Wait for all messages to be consumed (timeout 30s)
            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(MAX_ELAPSED_SECONDS)));
            var consumeSnapshot = consumeMetrics.Stop();
            consumeSnapshot.PrintSummary("CONSUME", messageCount, this);
            LogCpuWallRatio(consumeSnapshot, "CONSUME");

            _output.WriteLine($"Consumer received: {receivedCount} / {messageCount}");
            Assert.True(completed == allReceived.Task, $"Consumer timed out. Received {receivedCount}/{messageCount}");
            Assert.Equal(messageCount, receivedCount);

            // Queue should be drained
            await Task.Delay(500);
            int remaining = queue.Manager.MessageStore.Count();
            _output.WriteLine($"Messages remaining in store: {remaining}");
            Assert.Equal(0, remaining);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 2: Producer and consumer are both connected simultaneously.
    /// Messages flow through in real-time.
    /// Measures end-to-end throughput, CPU, RAM and CPU/Wall ratio under concurrent load.
    /// </summary>
    [Fact]
    public async Task ProducerAndConsumer_Simultaneous()
    {
        var (rider, server, port) = StartPushServer();

        try
        {
            const string queueName = "bench-push-simultaneous";
            const int messageCount = LARGE_BATCH; // 50,000

            // ── Setup consumer first ──
            int receivedCount = 0;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var consumer = await CreateClient(port, "bench-consumer-sim");
            Assert.True(consumer.IsConnected);

            consumer.MessageReceived += (_, _) =>
            {
                int count = Interlocked.Increment(ref receivedCount);
                if (count >= messageCount)
                    allReceived.TrySetResult(true);
            };

            var subResult = await consumer.Queue.Subscribe(queueName, true);
            Assert.Equal(HorseResultCode.Ok, subResult.Code);

            await Task.Delay(300); // Let subscription settle

            // ── Setup producer and start pushing ──
            var producer = await CreateClient(port, "bench-producer-sim");
            Assert.True(producer.IsConnected);

            var metrics = new BenchmarkMetrics();
            metrics.Start();

            for (int i = 0; i < messageCount; i++)
            {
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);
            }

            _output.WriteLine($"All {messageCount} messages pushed. Waiting for consumer to drain...");

            // Wait for all messages to be consumed
            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(MAX_ELAPSED_SECONDS)));
            var snapshot = metrics.Stop();
            snapshot.PrintSummary("SIMULTANEOUS", messageCount, this);
            LogCpuWallRatio(snapshot, "SIMULTANEOUS");

            _output.WriteLine($"Consumer received: {receivedCount} / {messageCount}");
            Assert.True(completed == allReceived.Task, $"Consumer timed out. Received {receivedCount}/{messageCount}");
            Assert.Equal(messageCount, receivedCount);

            // Assert thresholds
            double throughput = BenchmarkMetrics.Throughput(messageCount, snapshot.Elapsed);
            Assert.True(snapshot.Elapsed.TotalSeconds < MAX_ELAPSED_SECONDS,
                $"Total took {snapshot.Elapsed.TotalSeconds:F1}s, max allowed {MAX_ELAPSED_SECONDS}s");
            Assert.True(throughput >= MIN_THROUGHPUT,
                $"Throughput {throughput:F0} msg/sec below minimum {MIN_THROUGHPUT}");
            Assert.True(snapshot.RamDeltaMB < MAX_RAM_DELTA_MB,
                $"RAM delta {snapshot.RamDeltaMB:F1} MB exceeds {MAX_RAM_DELTA_MB} MB");
            Assert.True(snapshot.CpuTimeDelta.TotalSeconds < MAX_CPU_TIME_SECONDS,
                $"CPU time {snapshot.CpuTimeDelta.TotalSeconds:F1}s exceeds {MAX_CPU_TIME_SECONDS}s");

            // Queue should be empty or near-empty
            await Task.Delay(500);
            HorseQueue queue = rider.Queue.Find(queueName);
            if (queue != null)
            {
                int remaining = queue.Manager.MessageStore.Count();
                _output.WriteLine($"Messages remaining in store: {remaining}");
                Assert.True(remaining <= 10, $"Queue still has {remaining} messages, expected near-empty");
            }

            // Server-side stats
            if (queue != null)
            {
                _output.WriteLine($"Server ReceivedMessages: {queue.Info.ReceivedMessages}");
                _output.WriteLine($"Server SentMessages: {queue.Info.SentMessages}");
            }

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 3: Push state broadcasts to 3 consumers simultaneously.
    /// Unlike RoundRobin, Push sends each message to ALL consumers.
    /// 10,000 messages; each consumer should receive all of them.
    /// Measures CPU/Wall ratio under broadcast fan-out.
    /// </summary>
    [Fact]
    public async Task Push_3Consumers_Broadcast_CpuCheck()
    {
        var (_, server, port) = StartPushServer();

        try
        {
            const string queueName = "bench-push-broadcast";
            const int messageCount = 10_000;
            const int consumerCount = 3;

            var perConsumerCount = new int[consumerCount];
            int totalReceived = 0;
            int expectedTotal = messageCount * consumerCount;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // ── Connect 3 consumers ──
            var consumers = new HorseClient[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                int idx = c;
                consumers[c] = await CreateClient(port, $"push-bc-consumer-{c}");
                Assert.True(consumers[c].IsConnected);

                consumers[c].MessageReceived += (_, _) =>
                {
                    Interlocked.Increment(ref perConsumerCount[idx]);
                    int count = Interlocked.Increment(ref totalReceived);
                    if (count >= expectedTotal)
                        allReceived.TrySetResult(true);
                };

                var sub = await consumers[c].Queue.Subscribe(queueName, true);
                Assert.Equal(HorseResultCode.Ok, sub.Code);
            }

            await Task.Delay(300);

            // ── Producer pushes ──
            var producer = await CreateClient(port, "push-bc-producer");
            Assert.True(producer.IsConnected);

            var metrics = new BenchmarkMetrics();
            metrics.Start();

            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(MAX_ELAPSED_SECONDS)));
            var snapshot = metrics.Stop();
            snapshot.PrintSummary("PUSH-BROADCAST", messageCount, this);
            LogCpuWallRatio(snapshot, "PUSH-BROADCAST");

            for (int c = 0; c < consumerCount; c++)
                _output.WriteLine($"Consumer-{c} received: {perConsumerCount[c]}");

            _output.WriteLine($"Total received: {totalReceived} / {expectedTotal}");

            Assert.True(completed == allReceived.Task,
                $"Timed out. Received {totalReceived}/{expectedTotal}");

            // Each consumer should get ALL messages (Push = broadcast)
            foreach (int count in perConsumerCount)
                Assert.Equal(messageCount, count);

            Assert.True(snapshot.CpuTimeDelta.TotalSeconds < MAX_CPU_TIME_SECONDS,
                $"CPU time {snapshot.CpuTimeDelta.TotalSeconds:F1}s exceeds {MAX_CPU_TIME_SECONDS}s");
            Assert.True(snapshot.RamDeltaMB < MAX_RAM_DELTA_MB,
                $"RAM delta {snapshot.RamDeltaMB:F1} MB exceeds {MAX_RAM_DELTA_MB} MB");

            producer.Disconnect();
            foreach (var c in consumers) c.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 4: Push + WaitForAcknowledge + 3 consumers + slow ACK.
    /// Each consumer delays 50ms before sending ACK.
    /// Push broadcasts to all 3, then waits for ACK from all before processing next.
    /// This creates a window where the server might spin-wait.
    /// 500 messages (smaller due to ACK overhead).
    /// </summary>
    [Fact]
    public async Task Push_WaitForAck_3Consumers_SlowAck_CpuSpinDetection()
    {
        var (_, server, port) = StartPushServer(QueueAckDecision.WaitForAcknowledge);

        try
        {
            const string queueName = "bench-push-slow-ack";
            const int messageCount = 500;
            const int consumerCount = 3;
            const int consumerDelayMs = 50;

            var perConsumerCount = new int[consumerCount];
            int totalReceived = 0;
            int expectedTotal = messageCount * consumerCount;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // ── Connect 3 consumers with simulated processing delay ──
            var consumers = new HorseClient[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                int idx = c;
                consumers[c] = await CreateClient(port, $"push-ack-consumer-{c}");
                Assert.True(consumers[c].IsConnected);

                consumers[c].MessageReceived += async (client, msg) =>
                {
                    await Task.Delay(consumerDelayMs);
                    await client.SendAck(msg);

                    Interlocked.Increment(ref perConsumerCount[idx]);
                    int count = Interlocked.Increment(ref totalReceived);
                    if (count >= expectedTotal)
                        allReceived.TrySetResult(true);
                };

                var sub = await consumers[c].Queue.Subscribe(queueName, true);
                Assert.Equal(HorseResultCode.Ok, sub.Code);
            }

            await Task.Delay(300);

            var producer = await CreateClient(port, "push-ack-producer");
            Assert.True(producer.IsConnected);

            var metrics = new BenchmarkMetrics();
            metrics.Start();

            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            _output.WriteLine($"All {messageCount} messages pushed.");

            // Theoretical: 500 msgs * 50ms = ~25s (serial ACK wait)
            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(60)));
            var snapshot = metrics.Stop();
            snapshot.PrintSummary("PUSH-SLOW-ACK", messageCount, this);

            for (int c = 0; c < consumerCount; c++)
                _output.WriteLine($"Consumer-{c} received: {perConsumerCount[c]}");

            _output.WriteLine($"Total received: {totalReceived} / {expectedTotal}");

            Assert.True(completed == allReceived.Task,
                $"Timed out. Received {totalReceived}/{expectedTotal}");

            // CPU/Wall ratio — key check for spin-wait detection
            LogCpuWallRatio(snapshot, "PUSH-SLOW-ACK");

            Assert.True(snapshot.CpuTimeDelta.TotalSeconds < MAX_CPU_TIME_SECONDS,
                $"CPU time {snapshot.CpuTimeDelta.TotalSeconds:F1}s exceeds {MAX_CPU_TIME_SECONDS}s");

            producer.Disconnect();
            foreach (var c in consumers) c.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 5: Push + WaitForAcknowledge + 3 consumers + 200ms delay (heavy busy).
    /// Burst of 300 messages. All consumers are busy most of the time.
    /// Specifically targets the delivery tracker's while(!tracked) spin
    /// and any ACK-waiting hot path.
    /// </summary>
    [Fact]
    public async Task Push_WaitForAck_AllConsumersBusy_BurstCpuCheck()
    {
        var (_, server, port) = StartPushServer(QueueAckDecision.WaitForAcknowledge);

        try
        {
            const string queueName = "bench-push-busy-burst";
            const int messageCount = 300;
            const int consumerCount = 3;
            const int consumerDelayMs = 200;

            var perConsumerCount = new int[consumerCount];
            int totalReceived = 0;
            int expectedTotal = messageCount * consumerCount;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // ── Connect 3 consumers with 200ms processing delay ──
            var consumers = new HorseClient[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                int idx = c;
                consumers[c] = await CreateClient(port, $"push-busy-consumer-{c}");
                Assert.True(consumers[c].IsConnected);

                consumers[c].MessageReceived += async (client, msg) =>
                {
                    await Task.Delay(consumerDelayMs);
                    await client.SendAck(msg);

                    Interlocked.Increment(ref perConsumerCount[idx]);
                    int count = Interlocked.Increment(ref totalReceived);
                    if (count >= expectedTotal)
                        allReceived.TrySetResult(true);
                };

                var sub = await consumers[c].Queue.Subscribe(queueName, true);
                Assert.Equal(HorseResultCode.Ok, sub.Code);
            }

            await Task.Delay(300);

            var producer = await CreateClient(port, "push-busy-producer");
            Assert.True(producer.IsConnected);

            var metrics = new BenchmarkMetrics();
            metrics.Start();

            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            _output.WriteLine($"All {messageCount} messages pushed in burst.");

            // Theoretical: 300 msgs * 200ms = ~60s max
            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(90)));
            var snapshot = metrics.Stop();
            snapshot.PrintSummary("PUSH-BUSY-BURST", messageCount, this);

            for (int c = 0; c < consumerCount; c++)
                _output.WriteLine($"Consumer-{c} received: {perConsumerCount[c]}");

            _output.WriteLine($"Total received: {totalReceived} / {expectedTotal}");

            Assert.True(completed == allReceived.Task,
                $"Timed out. Received {totalReceived}/{expectedTotal}");

            // CPU/Wall — diagnostic only (multi-thread in-process test)
            double cpuRatio = snapshot.Elapsed.TotalSeconds > 0
                ? snapshot.CpuTimeDelta.TotalSeconds / snapshot.Elapsed.TotalSeconds
                : 0;

            _output.WriteLine($"CPU/Wall ratio: {cpuRatio:F2} (diagnostic only)");
            _output.WriteLine($"Wall: {snapshot.Elapsed.TotalSeconds:F1}s, CPU: {snapshot.CpuTimeDelta.TotalSeconds:F1}s");


            producer.Disconnect();
            foreach (var c in consumers) c.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }
}

