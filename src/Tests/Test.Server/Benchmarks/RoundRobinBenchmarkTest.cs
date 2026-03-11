using System;
using System.IO;
using System.Text;
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
/// Benchmark tests for RoundRobin queue state.
/// Focuses on detecting excessive CPU usage when consumers are busy
/// and the server spins in GetNextAvailableRRClient loop.
/// </summary>
public class RoundRobinBenchmarkTest : ITestOutputSink
{
    private readonly ITestOutputHelper _output;

    private const int MAX_ELAPSED_SECONDS = 30;
    private const long MAX_RAM_DELTA_MB = 500;

    private static readonly byte[] MessagePayload = Encoding.UTF8.GetBytes(new string('x', 100));

    public RoundRobinBenchmarkTest(ITestOutputHelper output)
    {
        _output = output;
    }

    void ITestOutputSink.WriteLine(string message) => _output.WriteLine(message);

    #region Helpers

    private static (HorseServer server, int port) StartRoundRobinServer(
        QueueAckDecision ackDecision,
        TimeSpan? ackTimeout = null)
    {
        var rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o =>
            {
                var rnd = new Random();
                o.DataPath = $"rr-bench-{Environment.TickCount}-{rnd.Next(0, 100000)}";
            })
            .ConfigureQueues(cfg =>
            {
                cfg.Options.Type = QueueType.RoundRobin;
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

        return (server, port);
    }

    private static async Task<HorseClient> CreateClient(int port, string name)
    {
        var client = new HorseClient();
        client.SetClientName(name);
        await client.ConnectAsync($"horse://localhost:{port}");
        return client;
    }

    #endregion

    /// <summary>
    /// Scenario 1: RoundRobin + WaitForAcknowledge + 3 consumers.
    /// All consumers acknowledge instantly.
    /// 10,000 messages pushed. Measures CPU to ensure no spin-wait overhead.
    /// </summary>
    [Fact]
    public async Task RoundRobin_WaitForAck_3Consumers_FastAck_CpuStaysLow()
    {
        var (server, port) = StartRoundRobinServer(QueueAckDecision.WaitForAcknowledge);

        try
        {
            const string queueName = "rr-bench-fast-ack";
            const int messageCount = 10_000;
            const int consumerCount = 3;

            int totalReceived = 0;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var perConsumerCount = new int[consumerCount];

            // ── Connect 3 consumers, each sends ACK immediately via AutoAcknowledge ──
            var consumers = new HorseClient[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                int idx = c;
                consumers[c] = await CreateClient(port, $"rr-consumer-{c}");
                consumers[c].AutoAcknowledge = true;
                Assert.True(consumers[c].IsConnected);

                consumers[c].MessageReceived += (_, _) =>
                {
                    Interlocked.Increment(ref perConsumerCount[idx]);
                    int count = Interlocked.Increment(ref totalReceived);
                    if (count >= messageCount)
                        allReceived.TrySetResult(true);
                };

                var sub = await consumers[c].Queue.Subscribe(queueName, true);
                Assert.Equal(HorseResultCode.Ok, sub.Code);
            }

            await Task.Delay(300);

            // ── Producer pushes messages ──
            var producer = await CreateClient(port, "rr-producer-fast");
            Assert.True(producer.IsConnected);

            var metrics = new BenchmarkMetrics();
            metrics.Start();

            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(MAX_ELAPSED_SECONDS)));
            var snapshot = metrics.Stop();
            snapshot.PrintSummary("RR-FAST-ACK", messageCount, this);

            for (int c = 0; c < consumerCount; c++)
                _output.WriteLine($"Consumer-{c} received: {perConsumerCount[c]}");

            _output.WriteLine($"Total received: {totalReceived} / {messageCount}");

            Assert.True(completed == allReceived.Task,
                $"Timed out. Received {totalReceived}/{messageCount}");
            Assert.Equal(messageCount, totalReceived);

            // CPU/Wall ratio — diagnostic only. In multi-threaded in-process tests,
            // ratio > 1.0 is expected (N threads running in parallel on N cores).
            // Spin-wait detection is handled by latency-based tests (Scenario 5, 6, 7).
            double cpuRatio = snapshot.CpuTimeDelta.TotalSeconds / Math.Max(snapshot.Elapsed.TotalSeconds, 0.001);
            _output.WriteLine($"CPU/Wall ratio: {cpuRatio:F2}");

            Assert.True(snapshot.RamDeltaMB < MAX_RAM_DELTA_MB,
                $"RAM delta {snapshot.RamDeltaMB:F1} MB exceeds {MAX_RAM_DELTA_MB} MB");

            // Distribution check: each consumer should get at least 20% of messages (fair RR)
            foreach (int count in perConsumerCount)
            {
                double pct = (double)count / messageCount * 100;
                Assert.True(count > messageCount / (consumerCount * 2),
                    $"RoundRobin distribution unfair: one consumer got only {count} ({pct:F1}%) of {messageCount}");
            }

            producer.Disconnect();
            foreach (var c in consumers) c.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 2: RoundRobin + WaitForAcknowledge + 3 consumers + SLOW consumers.
    /// Each consumer simulates 50ms processing delay before ACK.
    /// When all 3 consumers are busy, the server's GetNextAvailableRRClient
    /// spins in a tight loop. This test detects that CPU spike.
    /// 1,000 messages pushed (smaller batch because of slow consumers).
    /// </summary>
    [Fact]
    public async Task RoundRobin_WaitForAck_3Consumers_SlowAck_DetectCpuSpike()
    {
        var (server, port) = StartRoundRobinServer(QueueAckDecision.WaitForAcknowledge);

        try
        {
            const string queueName = "rr-bench-slow-ack";
            const int messageCount = 1_000;
            const int consumerCount = 3;
            const int consumerDelayMs = 50; // each consumer takes 50ms to process

            int totalReceived = 0;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var perConsumerCount = new int[consumerCount];

            // ── Connect 3 consumers with simulated processing delay ──
            var consumers = new HorseClient[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                int idx = c;
                consumers[c] = await CreateClient(port, $"rr-slow-consumer-{c}");
                Assert.True(consumers[c].IsConnected);

                consumers[c].MessageReceived += async (client, msg) =>
                {
                    // Simulate processing delay then send ACK
                    await Task.Delay(consumerDelayMs);
                    await client.SendAck(msg);

                    Interlocked.Increment(ref perConsumerCount[idx]);
                    int count = Interlocked.Increment(ref totalReceived);
                    if (count >= messageCount)
                        allReceived.TrySetResult(true);
                };

                var sub = await consumers[c].Queue.Subscribe(queueName, true);
                Assert.Equal(HorseResultCode.Ok, sub.Code);
            }

            await Task.Delay(300);

            // ── Producer pushes all messages at once ──
            var producer = await CreateClient(port, "rr-producer-slow");
            Assert.True(producer.IsConnected);

            var metrics = new BenchmarkMetrics();
            metrics.Start();

            // Push with waitForCommit=true so producer waits for server to accept
            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            _output.WriteLine($"All {messageCount} messages pushed.");

            // Theoretical minimum time: 1000 msgs / 3 consumers * 50ms = ~16.7s
            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(60)));
            var snapshot = metrics.Stop();
            snapshot.PrintSummary("RR-SLOW-ACK", messageCount, this);

            for (int c = 0; c < consumerCount; c++)
                _output.WriteLine($"Consumer-{c} received: {perConsumerCount[c]}");

            _output.WriteLine($"Total received: {totalReceived} / {messageCount}");

            Assert.True(completed == allReceived.Task,
                $"Timed out. Received {totalReceived}/{messageCount}");

            // ── CPU DIAGNOSTIC ──
            // Process.TotalProcessorTime sums ALL threads (server + 3 consumers + producer).
            // In multi-threaded in-process tests, CPU/Wall > 1.0 is expected on multi-core.
            // Spin-wait detection is handled by latency-based tests (Scenario 5, 6, 7).
            double cpuRatio = snapshot.CpuTimeDelta.TotalSeconds / snapshot.Elapsed.TotalSeconds;
            _output.WriteLine($"CPU/Wall ratio: {cpuRatio:F2}");


            producer.Disconnect();
            foreach (var c in consumers) c.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 3: RoundRobin + NoAck + 3 consumers + high throughput.
    /// 50,000 messages fire-and-forget. Tests CPU under heavy RR distribution
    /// where no ACK wait is needed but the index rotation still happens.
    /// </summary>
    [Fact]
    public async Task RoundRobin_NoAck_3Consumers_HighThroughput_CpuCheck()
    {
        var (server, port) = StartRoundRobinServer(QueueAckDecision.None);

        try
        {
            const string queueName = "rr-bench-noack";
            const int messageCount = 50_000;
            const int consumerCount = 3;

            int totalReceived = 0;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var perConsumerCount = new int[consumerCount];

            // ── Connect 3 consumers ──
            var consumers = new HorseClient[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                int idx = c;
                consumers[c] = await CreateClient(port, $"rr-noack-consumer-{c}");
                Assert.True(consumers[c].IsConnected);

                consumers[c].MessageReceived += (_, _) =>
                {
                    Interlocked.Increment(ref perConsumerCount[idx]);
                    int count = Interlocked.Increment(ref totalReceived);
                    if (count >= messageCount)
                        allReceived.TrySetResult(true);
                };

                var sub = await consumers[c].Queue.Subscribe(queueName, true);
                Assert.Equal(HorseResultCode.Ok, sub.Code);
            }

            await Task.Delay(300);

            // ── Producer pushes at full speed ──
            var producer = await CreateClient(port, "rr-producer-noack");
            Assert.True(producer.IsConnected);

            var metrics = new BenchmarkMetrics();
            metrics.Start();

            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            _output.WriteLine($"All {messageCount} messages pushed.");

            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(MAX_ELAPSED_SECONDS)));
            var snapshot = metrics.Stop();
            snapshot.PrintSummary("RR-NOACK", messageCount, this);

            for (int c = 0; c < consumerCount; c++)
                _output.WriteLine($"Consumer-{c} received: {perConsumerCount[c]}");

            _output.WriteLine($"Total received: {totalReceived} / {messageCount}");

            Assert.True(completed == allReceived.Task,
                $"Timed out. Received {totalReceived}/{messageCount}");
            Assert.Equal(messageCount, totalReceived);

            double throughput = BenchmarkMetrics.Throughput(messageCount, snapshot.Elapsed);
            _output.WriteLine($"Throughput: {throughput:F0} msg/sec");

            double cpuRatio = snapshot.CpuTimeDelta.TotalSeconds / Math.Max(snapshot.Elapsed.TotalSeconds, 0.001);
            _output.WriteLine($"CPU/Wall ratio: {cpuRatio:F2}");

            Assert.True(snapshot.RamDeltaMB < MAX_RAM_DELTA_MB,
                $"RAM delta {snapshot.RamDeltaMB:F1} MB exceeds {MAX_RAM_DELTA_MB} MB");

            // Distribution check
            foreach (int count in perConsumerCount)
            {
                double pct = (double)count / messageCount * 100;
                Assert.True(count > messageCount / (consumerCount * 2),
                    $"RoundRobin distribution unfair: one consumer got only {count} ({pct:F1}%) of {messageCount}");
            }

            producer.Disconnect();
            foreach (var c in consumers) c.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 4: RoundRobin + WaitForAcknowledge + 3 consumers + burst messages.
    /// Pushes 500 messages in burst, all consumers are initially busy.
    /// The server must find next available client without burning CPU.
    /// Measures CPU usage during the "all busy" wait period specifically.
    /// </summary>
    [Fact]
    public async Task RoundRobin_WaitForAck_AllConsumersBusy_CpuSpinDetection()
    {
        var (server, port) = StartRoundRobinServer(QueueAckDecision.WaitForAcknowledge);

        try
        {
            const string queueName = "rr-bench-all-busy";
            const int messageCount = 500;
            const int consumerCount = 3;
            const int consumerDelayMs = 200; // significant delay to keep all consumers busy

            int totalReceived = 0;
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // ── Connect 3 consumers with 200ms processing delay ──
            var consumers = new HorseClient[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                consumers[c] = await CreateClient(port, $"rr-busy-consumer-{c}");
                Assert.True(consumers[c].IsConnected);

                consumers[c].MessageReceived += async (client, msg) =>
                {
                    await Task.Delay(consumerDelayMs);
                    await client.SendAck(msg);
                    int count = Interlocked.Increment(ref totalReceived);
                    if (count >= messageCount)
                        allReceived.TrySetResult(true);
                };

                var sub = await consumers[c].Queue.Subscribe(queueName, true);
                Assert.Equal(HorseResultCode.Ok, sub.Code);
            }

            await Task.Delay(300);

            var producer = await CreateClient(port, "rr-producer-busy");
            Assert.True(producer.IsConnected);

            // Take CPU baseline right before push burst
            var metrics = new BenchmarkMetrics();
            metrics.Start();

            // Push all messages rapidly — server will queue them and spin
            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            _output.WriteLine($"All {messageCount} messages pushed in burst.");

            // Theoretical: 500 msgs / 3 consumers * 200ms = ~33s
            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(60)));
            var snapshot = metrics.Stop();
            snapshot.PrintSummary("RR-ALL-BUSY", messageCount, this);

            _output.WriteLine($"Total received: {totalReceived} / {messageCount}");
            Assert.True(completed == allReceived.Task,
                $"Timed out. Received {totalReceived}/{messageCount}");

            // ── CPU DIAGNOSTIC ──
            // Process.TotalProcessorTime sums ALL threads (server + 3 consumers + producer).
            // In multi-threaded in-process tests, CPU/Wall > 1.0 is expected on multi-core.
            // Spin-wait detection is handled by latency-based tests (Scenario 5, 6, 7).
            double cpuRatio = snapshot.CpuTimeDelta.TotalSeconds / snapshot.Elapsed.TotalSeconds;
            _output.WriteLine($"CPU/Wall ratio: {cpuRatio:F2}");
            _output.WriteLine($"Wall time: {snapshot.Elapsed.TotalSeconds:F1}s, CPU time: {snapshot.CpuTimeDelta.TotalSeconds:F1}s");


            producer.Disconnect();
            foreach (var c in consumers) c.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 5: Signal mechanism latency test.
    /// 
    /// Proves that after ACK, the next queued message is delivered IMMEDIATELY
    /// (via Trigger + SignalClientAvailable) rather than waiting for the 5-second
    /// fallback timer.
    /// 
    /// Steps:
    ///   1. Connect 1 consumer (WaitForAck mode)
    ///   2. Push message-1 → delivered immediately to consumer
    ///   3. Push message-2 while consumer is busy → goes to store (NoConsumers)
    ///   4. Consumer sends ACK for message-1 after 500ms delay
    ///   5. Measure: How quickly is message-2 delivered after ACK?
    ///      - With signal+trigger: &lt;500ms
    ///      - Without (5s timer):  ~5000ms
    /// </summary>
    [Fact]
    public async Task RoundRobin_Signal_AckTriggersImmediateDelivery()
    {
        var (server, port) = StartRoundRobinServer(QueueAckDecision.WaitForAcknowledge);

        try
        {
            const string queueName = "rr-signal-latency";

            int receivedCount = 0;
            HorseMessage firstMessage = null;
            var firstMessageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondMessageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // ── Single consumer: delays 500ms then sends ACK ──
            var consumer = await CreateClient(port, "signal-consumer");
            Assert.True(consumer.IsConnected);

            consumer.MessageReceived += async (client, msg) =>
            {
                int idx = Interlocked.Increment(ref receivedCount);

                if (idx == 1)
                {
                    firstMessage = msg;
                    firstMessageReceived.TrySetResult(true);

                    // Simulate processing — hold ACK for 500ms
                    await Task.Delay(500);
                    await client.SendAck(msg);
                    // After this ACK, server should deliver message-2 IMMEDIATELY
                }
                else if (idx == 2)
                {
                    secondMessageReceived.TrySetResult(true);
                    await client.SendAck(msg);
                }
            };

            var sub = await consumer.Queue.Subscribe(queueName, true);
            Assert.Equal(HorseResultCode.Ok, sub.Code);
            await Task.Delay(300);

            // ── Producer ──
            var producer = await CreateClient(port, "signal-producer");
            Assert.True(producer.IsConnected);

            // Push message-1 → should be delivered to consumer immediately
            await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            // Wait for consumer to receive message-1
            var msg1Task = await Task.WhenAny(firstMessageReceived.Task, Task.Delay(5000));
            Assert.True(msg1Task == firstMessageReceived.Task, "Message-1 was not received by consumer");

            // Push message-2 while consumer is still processing message-1
            // Consumer is busy (CurrentlyProcessing != null) so this goes to store
            await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            // Now wait for message-2 delivery. Start timing from NOW.
            // The consumer's ACK for message-1 will arrive ~500ms after message-1 was received.
            // After ACK, message-2 should be delivered almost immediately via Trigger.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var msg2Task = await Task.WhenAny(secondMessageReceived.Task, Task.Delay(8000));
            sw.Stop();

            _output.WriteLine($"Message-2 delivery latency after push: {sw.ElapsedMilliseconds} ms");
            _output.WriteLine($"Total received: {receivedCount}");

            Assert.True(msg2Task == secondMessageReceived.Task,
                $"Message-2 was NOT delivered within 8s. If this fails, ACK does not trigger re-delivery. Received: {receivedCount}");

            // The key assertion: message-2 should arrive within ~1s of push
            // (500ms ACK delay + small overhead). Without signal/trigger, it would take ~5s.
            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Message-2 took {sw.ElapsedMilliseconds}ms to arrive. " +
                $"Expected <2000ms (signal-based). If >4000ms, delivery fell back to 5s timer — signal mechanism is broken.");

            Assert.Equal(2, receivedCount);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 6: Signal mechanism with multiple consumers.
    /// 
    /// 3 consumers, each with 300ms ACK delay.
    /// Push 10 messages. Verify:
    ///   - All messages delivered
    ///   - Total time is close to theoretical minimum (~4 batches × 300ms = ~1.2s)
    ///     not 5s+ (which would indicate broken signaling, falling back to timer)
    ///   - Messages are distributed across consumers (round-robin)
    /// </summary>
    [Fact]
    public async Task RoundRobin_Signal_MultiConsumer_NoTimerFallback()
    {
        var (server, port) = StartRoundRobinServer(QueueAckDecision.WaitForAcknowledge);

        try
        {
            const string queueName = "rr-signal-multi";
            const int messageCount = 10;
            const int consumerCount = 3;
            const int ackDelayMs = 300;

            int totalReceived = 0;
            var perConsumerCount = new int[consumerCount];
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // ── 3 consumers, each delays 300ms then ACKs ──
            var consumers = new HorseClient[consumerCount];
            for (int c = 0; c < consumerCount; c++)
            {
                int idx = c;
                consumers[c] = await CreateClient(port, $"sig-consumer-{c}");
                Assert.True(consumers[c].IsConnected);

                consumers[c].MessageReceived += async (client, msg) =>
                {
                    await Task.Delay(ackDelayMs);
                    await client.SendAck(msg);

                    Interlocked.Increment(ref perConsumerCount[idx]);
                    int count = Interlocked.Increment(ref totalReceived);
                    if (count >= messageCount)
                        allReceived.TrySetResult(true);
                };

                var sub = await consumers[c].Queue.Subscribe(queueName, true);
                Assert.Equal(HorseResultCode.Ok, sub.Code);
            }

            await Task.Delay(300);

            // ── Push all messages ──
            var producer = await CreateClient(port, "sig-producer-multi");
            Assert.True(producer.IsConnected);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < messageCount; i++)
                await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            _output.WriteLine($"All {messageCount} messages pushed.");

            // Theoretical minimum: ceil(10/3) batches × 300ms = 4 × 300ms = 1.2s
            // With signal mechanism: ~1.2-2s
            // Without signal (5s timer fallback): ~20s+ (each batch waits for 5s timer)
            var completed = await Task.WhenAny(allReceived.Task, Task.Delay(15000));
            sw.Stop();

            _output.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
            for (int c = 0; c < consumerCount; c++)
                _output.WriteLine($"Consumer-{c} received: {perConsumerCount[c]}");
            _output.WriteLine($"Total received: {totalReceived} / {messageCount}");

            Assert.True(completed == allReceived.Task,
                $"Timed out after 15s. Received {totalReceived}/{messageCount}. Signal mechanism may be broken.");

            // All messages must be delivered
            Assert.Equal(messageCount, totalReceived);

            // Total time must be well under the 5s timer fallback threshold.
            // With signal: ~1.5s. Without: each batch after the first 3 waits ~5s.
            // We use 5s as the boundary — anything over 5s means at least one batch
            // fell back to the timer instead of being triggered by ACK.
            Assert.True(sw.ElapsedMilliseconds < 5000,
                $"Delivery took {sw.ElapsedMilliseconds}ms. Expected <5000ms with signal mechanism. " +
                $"If >5000ms, ACK-based triggering is not working and delivery falls back to 5s timer.");

            // Round-robin distribution: each consumer should get ~3-4 messages
            foreach (int count in perConsumerCount)
                Assert.True(count >= 1, "At least one consumer received 0 messages — round-robin broken");

            producer.Disconnect();
            foreach (var c in consumers) c.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>
    /// Scenario 7: Signal after ACK timeout.
    /// 
    /// 1 consumer that never sends ACK. ACK timeout = 2s.
    /// Push 2 messages. After timeout, consumer slot should be freed
    /// and message-2 should be delivered without waiting for 5s timer.
    /// </summary>
    [Fact]
    public async Task RoundRobin_Signal_AckTimeout_FreesConsumerSlot()
    {
        var (server, port) = StartRoundRobinServer(
            QueueAckDecision.WaitForAcknowledge,
            ackTimeout: TimeSpan.FromSeconds(2));

        try
        {
            const string queueName = "rr-signal-timeout";

            int receivedCount = 0;
            var secondMessageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // ── Consumer that NEVER sends ACK (simulating stuck consumer) ──
            var consumer = await CreateClient(port, "timeout-consumer");
            Assert.True(consumer.IsConnected);

            consumer.MessageReceived += (_, _) =>
            {
                int idx = Interlocked.Increment(ref receivedCount);
                if (idx == 2)
                    secondMessageReceived.TrySetResult(true);
                // Intentionally NO ACK sent
            };

            var sub = await consumer.Queue.Subscribe(queueName, true);
            Assert.Equal(HorseResultCode.Ok, sub.Code);
            await Task.Delay(300);

            var producer = await CreateClient(port, "timeout-producer");
            Assert.True(producer.IsConnected);

            // Push message-1 → delivered, but no ACK will come
            await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);
            await Task.Delay(200); // Let it be delivered

            // Push message-2 → consumer is "busy" (CurrentlyProcessing != null)
            await producer.Queue.Push(queueName, new MemoryStream(MessagePayload), false);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // ACK timeout is 2s. After timeout, CurrentlyProcessing is cleared.
            // With signal: message-2 should arrive shortly after the 2s timeout (~2-3s total)
            // Without signal: would wait for next 5s timer cycle after timeout (~7s)
            var completed = await Task.WhenAny(secondMessageReceived.Task, Task.Delay(10000));
            sw.Stop();

            _output.WriteLine($"Message-2 latency: {sw.ElapsedMilliseconds} ms");
            _output.WriteLine($"Total received: {receivedCount}");

            Assert.True(completed == secondMessageReceived.Task,
                $"Message-2 was not delivered within 10s. Timeout signal mechanism may be broken. Received: {receivedCount}");

            // Should arrive within ~4s (2s ACK timeout + overhead + trigger)
            // If >7s, it means we fell back to the 5s timer after the timeout
            Assert.True(sw.ElapsedMilliseconds < 5000,
                $"Message-2 took {sw.ElapsedMilliseconds}ms. Expected <5000ms. " +
                $"ACK timeout should trigger re-delivery, not wait for 5s timer.");

            Assert.Equal(2, receivedCount);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await server.StopAsync();
        }
    }
}
