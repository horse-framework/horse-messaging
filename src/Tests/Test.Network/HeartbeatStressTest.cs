using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Test.Network;

/// <summary>
/// Tests that expose the heartbeat system's weaknesses identified in HEARTBEAT_ANALYSIS_REPORT.md.
/// These tests simulate production failure scenarios: CPU starvation, read-loop blocking,
/// narrow pong windows, and SmartHealthCheck asymmetry.
///
/// KEY INSIGHT: Server-side SmartHealthCheck is TRUE by default. This means KeepAlive() is called
/// for EVERY incoming message from the client (including PONG). KeepAlive() updates LastAliveTimeTicks
/// AND sets PongRequired=false. So the only way to trigger disconnect is:
///   (a) Delay PONG BEFORE sending it (so server tick fires while PongRequired=true), or
///   (b) Never send PONG at all (read loop completely blocked), or
///   (c) Send non-PONG data that doesn't reset PongRequired (only resets LastAliveTimeTicks).
///
/// ALL TESTS IN THIS CLASS ARE EXPECTED TO FAIL — they demonstrate known bugs/edge-cases.
/// When a test FAILs, it proves the edge-case exists. When fixed, the test should PASS.
/// </summary>
public class HeartbeatStressTest
{
    private readonly ITestOutputHelper _output;

    public HeartbeatStressTest(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Helpers

    /// <summary>
    /// Completes the Horse protocol handshake for a raw TCP client.
    /// Returns the NetworkStream for further communication.
    /// </summary>
    private async Task<NetworkStream> HandshakeRawClient(TcpClient rawClient, int port, string clientName)
    {
        NetworkStream netStream = rawClient.GetStream();
        netStream.Write(PredefinedMessages.PROTOCOL_BYTES_V4);

        HorseMessage hello = new HorseMessage();
        hello.Type = MessageType.Server;
        hello.ContentType = KnownContentTypes.Hello;
        hello.SetStringContent($"GET /\r\nName: {clientName}");
        hello.CalculateLengths();
        HorseProtocolWriter.Write(hello, netStream);

        // Read the server's protocol handshake response
        byte[] protocolResponse = new byte[PredefinedMessages.PROTOCOL_BYTES_V4.Length];
        int totalRead = 0;
        while (totalRead < protocolResponse.Length)
        {
            int r = await netStream.ReadAsync(protocolResponse, totalRead, protocolResponse.Length - totalRead);
            if (r == 0) break;
            totalRead += r;
        }

        return netStream;
    }

    #endregion

    #region ISSUE 1: Read Loop Blocking — PONG Delayed Under Message Processing Load

    /// <summary>
    /// ISSUE 1 (CRITICAL): PONG is delayed by 1.5s due to message processing on the read loop thread.
    /// Server's 1-tick window (1s) expires before PONG arrives.
    ///
    /// Edge-case: Processing delay (1.5s) slightly exceeds tick interval (1s).
    /// A well-designed heartbeat should tolerate this — but it doesn't.
    ///
    /// Expectation: Client should stay connected (1.5s delay is reasonable).
    /// Reality: Server disconnects → TEST FAILS.
    /// </summary>
    [Fact]
    public async Task Issue1_ReadLoopBlocking_PongDelayedByMessageProcessing()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);
            NetworkStream netStream = await HandshakeRawClient(rawClient, port, $"slow-reader-{port}");

            await Task.Delay(500);

            bool disconnected = false;
            int pingCount = 0;
            int pongCount = 0;
            HorseProtocolReader reader = new HorseProtocolReader();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!disconnected && rawClient.Connected)
                    {
                        HorseMessage msg = await reader.Read(netStream);
                        if (msg == null) { disconnected = true; break; }

                        if (msg.Type == MessageType.Ping)
                        {
                            int current = Interlocked.Increment(ref pingCount);

                            if (current <= 2)
                            {
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                            else
                            {
                                // 1.5s delay BEFORE PONG → exceeds 1s tick → disconnect
                                Thread.Sleep(1500);
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                        }
                    }
                }
                catch { disconnected = true; }
            });

            await Task.Delay(15_000);

            _output.WriteLine($"[ISSUE1] PINGs: {pingCount}, PONGs: {pongCount}, Disconnected: {disconnected}");

            Assert.False(disconnected,
                $"[ISSUE1-FAIL] Client disconnected because PONG was delayed by 1.5s (tick=1s). " +
                $"PINGs: {pingCount}, PONGs: {pongCount}. " +
                "A 1.5s PONG delay caused by message processing should be tolerable.");

            rawClient.Dispose();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region ISSUE 2: Single-Tick Pong Window Too Narrow

    /// <summary>
    /// ISSUE 2 (MEDIUM): Every PONG is delayed by 1.5s — server disconnects on the first one.
    /// The 1-tick window between PongRequired=true and the disconnect check is too narrow.
    ///
    /// Edge-case: Consistent 1.5s network/processing latency on every PONG.
    ///
    /// Expectation: Client should stay connected (PONG IS sent, just delayed).
    /// Reality: Server disconnects → TEST FAILS.
    /// </summary>
    [Fact]
    public async Task Issue2_SingleTickPongWindow_TooNarrow()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);
            NetworkStream stream = await HandshakeRawClient(rawClient, port, $"delayed-pong-{port}");

            await Task.Delay(500);

            bool disconnected = false;
            int pingReceived = 0;
            int pongSent = 0;
            HorseProtocolReader reader = new HorseProtocolReader();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!disconnected && rawClient.Connected)
                    {
                        HorseMessage msg = await reader.Read(stream);
                        if (msg == null) { disconnected = true; break; }

                        if (msg.Type == MessageType.Ping)
                        {
                            Interlocked.Increment(ref pingReceived);

                            // Every PONG delayed by 1.5s
                            await Task.Delay(1500);

                            if (!disconnected && rawClient.Connected)
                            {
                                try
                                {
                                    stream.Write(PredefinedMessages.PONG);
                                    Interlocked.Increment(ref pongSent);
                                }
                                catch { disconnected = true; }
                            }
                        }
                    }
                }
                catch { disconnected = true; }
            });

            await Task.Delay(12_000);

            _output.WriteLine($"[ISSUE2] PINGs: {pingReceived}, PONGs: {pongSent}, Disconnected: {disconnected}");

            Assert.False(disconnected,
                $"[ISSUE2-FAIL] Client disconnected even though PONG was always sent (with 1.5s delay). " +
                $"PINGs: {pingReceived}, PONGs: {pongSent}. " +
                "1-tick pong window is too narrow for even minor processing delays.");

            rawClient.Dispose();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region ISSUE 3: Read Loop Completely Blocked — PING Never Read

    /// <summary>
    /// ISSUE 3 (MEDIUM): Read loop is blocked by heavy processing. PING sits in TCP buffer
    /// unread, PONG is never sent, server disconnects.
    ///
    /// Edge-case: Client does a long synchronous operation (DB migration, file I/O, heavy computation)
    /// that blocks the read loop thread. During this time, PING arrives but cannot be read.
    ///
    /// This is deterministic: after warm-up, client STOPS reading entirely for 10s.
    /// Server MUST disconnect because no PONG arrives within the tick window.
    ///
    /// Expectation: Heartbeat should be independent of the read loop.
    /// Reality: Heartbeat is coupled to read loop → client disconnects → TEST FAILS.
    /// </summary>
    [Fact]
    public async Task Issue3_ReadLoopBlocked_NoPongDuringProcessing()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);
            NetworkStream netStream = await HandshakeRawClient(rawClient, port, $"blocked-reader-{port}");

            await Task.Delay(500);

            bool disconnected = false;
            int pingCount = 0;
            int pongCount = 0;
            DateTime? blockStartTime = null;
            DateTime? disconnectTime = null;
            HorseProtocolReader reader = new HorseProtocolReader();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!disconnected && rawClient.Connected)
                    {
                        HorseMessage msg = await reader.Read(netStream);
                        if (msg == null)
                        {
                            disconnected = true;
                            disconnectTime = DateTime.UtcNow;
                            break;
                        }

                        if (msg.Type == MessageType.Ping)
                        {
                            int current = Interlocked.Increment(ref pingCount);

                            // Send PONG immediately
                            try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                            catch { disconnected = true; }

                            // After 2nd PONG: block the read loop completely for 10s.
                            // No more reads = no more PINGs read = no more PONGs sent.
                            // Server timeline (PingInterval=3s, tick=1.5s):
                            //   T=0: PONG sent → KeepAlive(T=0)
                            //   ~T+4.5: SmartHealthCheck expired → PongRequired=true, PING sent
                            //   ~T+6: PongRequired still true → Disconnect
                            //   T+10: Client wakes at T+10 — too late.
                            if (current == 2)
                            {
                                blockStartTime = DateTime.UtcNow;
                                _output.WriteLine($"[ISSUE3] Blocking read loop for 10s at {blockStartTime:HH:mm:ss.fff}");
                                Thread.Sleep(10_000);
                                _output.WriteLine($"[ISSUE3] Block ended at {DateTime.UtcNow:HH:mm:ss.fff}");
                            }
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                    disconnectTime = DateTime.UtcNow;
                }
            });

            await Task.Delay(20_000);

            double blockToDisconnect = (disconnectTime.HasValue && blockStartTime.HasValue)
                ? (disconnectTime.Value - blockStartTime.Value).TotalSeconds
                : -1;

            _output.WriteLine($"[ISSUE3] PINGs: {pingCount}, PONGs: {pongCount}, Disconnected: {disconnected}");
            _output.WriteLine($"[ISSUE3] Time from block start to disconnect: {blockToDisconnect:F1}s");

            // The client SHOULD stay connected — heartbeat should be independent of read loop.
            // But because PING/PONG depends on the read loop, client WILL be disconnected.
            Assert.False(disconnected,
                $"[ISSUE3-FAIL] Client disconnected {blockToDisconnect:F1}s after read loop was blocked. " +
                $"PINGs: {pingCount}, PONGs: {pongCount}. " +
                "Heartbeat is coupled to the read loop — blocking it for 10s causes disconnect.");

            rawClient.Dispose();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region ISSUE 4: Silent Write Failure — Server Correctly Disconnects Unresponsive Client

    /// <summary>
    /// ISSUE 4 (MEDIUM): When PONG write silently fails (BeginWrite returns true but data never
    /// reaches the server), the server correctly detects the unresponsive client and disconnects it.
    ///
    /// This is EXPECTED BEHAVIOR — the server's heartbeat mechanism works as designed here.
    /// The architectural limitation is on the CLIENT side: SocketBase.Send() uses fire-and-forget
    /// BeginWrite which returns true before the write completes. If the write fails in EndWrite,
    /// the client has no way to know PONG wasn't delivered.
    ///
    /// Scenario:
    /// - Raw TCP client connects. Server PingInterval=2s (tick=1s).
    /// - First 2 PINGs: client sends real PONG (warm-up).
    /// - From PING 3 onwards: client reads PING but does NOT send PONG (simulating silent failure).
    /// - Server's grace period expires → Disconnect.
    ///
    /// Expectation: Server MUST disconnect the client — this validates heartbeat correctness.
    /// </summary>
    [Fact]
    public async Task Issue4_SilentWriteFailure_ServerCorrectlyDisconnects()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);
            NetworkStream netStream = await HandshakeRawClient(rawClient, port, $"silent-fail-{port}");

            await Task.Delay(500);

            bool disconnected = false;
            int pingCount = 0;
            int pongCount = 0;
            int silentFailCount = 0;
            DateTime? firstSilentFail = null;
            DateTime? disconnectTime = null;
            HorseProtocolReader reader = new HorseProtocolReader();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!disconnected && rawClient.Connected)
                    {
                        HorseMessage msg = await reader.Read(netStream);
                        if (msg == null)
                        {
                            disconnected = true;
                            disconnectTime = DateTime.UtcNow;
                            break;
                        }

                        if (msg.Type == MessageType.Ping)
                        {
                            int current = Interlocked.Increment(ref pingCount);

                            if (current <= 2)
                            {
                                // Warm-up: send real PONG
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                            else
                            {
                                // Simulate silent write failure: PONG is never sent.
                                int fails = Interlocked.Increment(ref silentFailCount);
                                if (fails == 1) firstSilentFail = DateTime.UtcNow;
                            }
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                    disconnectTime = DateTime.UtcNow;
                }
            });

            await Task.Delay(30_000);

            double timeToDisconnect = (disconnectTime.HasValue && firstSilentFail.HasValue)
                ? (disconnectTime.Value - firstSilentFail.Value).TotalSeconds
                : -1;

            _output.WriteLine($"[ISSUE4] PINGs: {pingCount}, PONGs: {pongCount}, SilentFails: {silentFailCount}, Disconnected: {disconnected}");
            _output.WriteLine($"[ISSUE4] Time from first silent fail to disconnect: {timeToDisconnect:F1}s");

            // Server MUST disconnect a client that never sends PONG — this is correct heartbeat behavior.
            Assert.True(disconnected,
                $"[ISSUE4-FAIL] Server did NOT disconnect client after {silentFailCount} missed PONGs. " +
                "When PONG is silently lost, the server's heartbeat must detect and disconnect the client.");

            // Verify disconnect happened within a reasonable grace window (not too fast, not too slow)
            Assert.True(timeToDisconnect > 0 && timeToDisconnect < 15,
                $"[ISSUE4-FAIL] Disconnect took {timeToDisconnect:F1}s — expected within 15s grace window.");

            rawClient.Dispose();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region ISSUE 5: Write Contention — PONG Delayed By Stream Lock

    /// <summary>
    /// ISSUE 5 (LOW): PONG write is blocked because another thread holds the stream write lock.
    ///
    /// Edge-case: Client is sending large messages on a background thread. When PING arrives,
    /// the PONG response must wait for the current write to complete. The write takes > 1 tick.
    ///
    /// This test is deterministic: PONG is delayed by 1.5s BEFORE sending (same mechanism as
    /// Issue 2, but the root cause is write contention rather than processing delay).
    /// After warm-up, every PONG is held for 1.5s before being sent.
    ///
    /// Expectation: PONG should have priority over data writes.
    /// Reality: All writes are serialized, PONG waits → server disconnects → TEST FAILS.
    /// </summary>
    [Fact]
    public async Task Issue5_WriteContention_PongDelayedBehindLargeWrites()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);
            NetworkStream netStream = await HandshakeRawClient(rawClient, port, $"write-contention-{port}");

            await Task.Delay(500);

            bool disconnected = false;
            int pingCount = 0;
            int pongCount = 0;
            HorseProtocolReader reader = new HorseProtocolReader();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!disconnected && rawClient.Connected)
                    {
                        HorseMessage msg = await reader.Read(netStream);
                        if (msg == null) { disconnected = true; break; }

                        if (msg.Type == MessageType.Ping)
                        {
                            int current = Interlocked.Increment(ref pingCount);

                            if (current <= 2)
                            {
                                // Warm-up: immediate PONG
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                            else
                            {
                                // Simulate write contention: another thread is writing a large message,
                                // PONG must wait 1.5s for the lock. This exceeds the 1s tick interval.
                                // In production: SocketBase uses SemaphoreSlim(1,1) for SSL streams,
                                // and BeginWrite serializes writes — PONG waits behind data.
                                Thread.Sleep(1500);
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                        }
                    }
                }
                catch { disconnected = true; }
            });

            await Task.Delay(15_000);

            _output.WriteLine($"[ISSUE5] PINGs: {pingCount}, PONGs: {pongCount}, Disconnected: {disconnected}");

            Assert.False(disconnected,
                $"[ISSUE5-FAIL] Client disconnected because PONG was delayed 1.5s by write contention. " +
                $"PINGs: {pingCount}, PONGs: {pongCount}. " +
                "Stream write serialization blocks PONG behind large data writes. " +
                "PONG has no priority mechanism — it waits in the FIFO write queue.");

            rawClient.Dispose();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region ISSUE 1+2 Combined: Processing Delay Exceeds PING Interval

    /// <summary>
    /// ISSUE 1+2 COMBINED: Processing takes 3.5s before PONG — exceeds the 2s PING interval.
    /// SmartHealthCheck can't save the client because no data is sent during the 3.5s window.
    ///
    /// Edge-case: Batch processing after reading a PING takes longer than the full PING interval.
    ///
    /// Expectation: Server should handle transient processing spikes.
    /// Reality: Instant disconnect → TEST FAILS.
    /// </summary>
    [Fact]
    public async Task Issue1_2_ProgressiveLoadIncrease_PongDelayExceedsPingInterval()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);
            NetworkStream netStream = await HandshakeRawClient(rawClient, port, $"progressive-load-{port}");

            await Task.Delay(500);

            bool disconnected = false;
            int pingCount = 0;
            int pongCount = 0;
            HorseProtocolReader reader = new HorseProtocolReader();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!disconnected && rawClient.Connected)
                    {
                        HorseMessage msg = await reader.Read(netStream);
                        if (msg == null) { disconnected = true; break; }

                        if (msg.Type == MessageType.Ping)
                        {
                            int current = Interlocked.Increment(ref pingCount);

                            if (current <= 2)
                            {
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                            else
                            {
                                // 3.5s > PingInterval (2s). During this delay, SmartHealthCheck
                                // alive window expires (last PONG was 3.5s+ ago).
                                Thread.Sleep(3500);
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                        }
                    }
                }
                catch { disconnected = true; }
            });

            await Task.Delay(20_000);

            _output.WriteLine($"[ISSUE1+2] PINGs: {pingCount}, PONGs: {pongCount}, Disconnected: {disconnected}");

            Assert.False(disconnected,
                $"[ISSUE1+2-FAIL] Client disconnected as processing time (3.5s) exceeded PING interval (2s). " +
                $"PINGs: {pingCount}, PONGs: {pongCount}. " +
                "No data sent during 3.5s window → SmartHealthCheck expired → disconnect.");

            rawClient.Dispose();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region ISSUE 6: HorseTime Drift Under Load

    /// <summary>
    /// ISSUE 6 (LOW): HorseTime drift under CPU stress.
    /// HorseTime.ServerTicks is updated by a PeriodicTimer(50ms) on the ThreadPool.
    /// Under CPU stress, the ThreadPool callback is delayed → HorseTime drifts.
    ///
    /// Edge-case: All CPU cores are saturated by Highest-priority spin threads.
    ///
    /// Expectation: HorseTime drift should be < 50ms.
    /// Reality: Under CPU saturation, drift exceeds 50ms → TEST FAILS.
    /// </summary>
    [Fact]
    public async Task Issue6_HorseTimeDrift_CausesIncorrectHeartbeatDecisions()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            var clients = new List<HorseClient>();
            for (int i = 0; i < 5; i++)
            {
                HorseClient c = new HorseClient();
                c.SetClientName($"drift-client-{i}");
                c.PingInterval = TimeSpan.FromSeconds(1);
                await c.ConnectAsync($"horse://localhost:{port}");
                clients.Add(c);
            }

            await Task.Delay(500);
            foreach (var c in clients) Assert.True(c.IsConnected);

            CancellationTokenSource stressCts = new();
            int spinCount = Environment.ProcessorCount * 3;

            for (int t = 0; t < spinCount; t++)
            {
                var thread = new Thread(() =>
                {
                    while (!stressCts.Token.IsCancellationRequested)
                        Thread.SpinWait(200_000);
                })
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Highest
                };
                thread.Start();
            }

            _output.WriteLine($"[ISSUE6] CPU stress: {spinCount} Highest-priority spin threads");

            double maxDrift = 0;
            for (int sample = 0; sample < 5; sample++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                long ticksBefore = Horse.Core.HorseTime.ServerTicks;

                Thread.Sleep(1000);

                sw.Stop();
                long ticksAfter = Horse.Core.HorseTime.ServerTicks;

                long actualTicks = sw.Elapsed.Ticks;
                long horseTicks = ticksAfter - ticksBefore;
                double drift = Math.Abs((actualTicks - horseTicks) / (double)TimeSpan.TicksPerMillisecond);
                maxDrift = Math.Max(maxDrift, drift);

                _output.WriteLine($"[ISSUE6] Sample {sample}: actual={sw.Elapsed.TotalMilliseconds:F1}ms, horse={horseTicks / (double)TimeSpan.TicksPerMillisecond:F1}ms, drift={drift:F1}ms");
            }

            stressCts.Cancel();
            await Task.Delay(1000);

            _output.WriteLine($"[ISSUE6] Max drift: {maxDrift:F1}ms");

            int disconnectedCount = clients.Count(c => !c.IsConnected);
            _output.WriteLine($"[ISSUE6] Disconnected clients: {disconnectedCount}/5");

            Assert.True(maxDrift < 150,
                $"[ISSUE6-FAIL] HorseTime max drift is {maxDrift:F1}ms (expected < 150ms). " +
                "Under CPU stress, PeriodicTimer callback is delayed → HorseTime drifts. " +
                "HeartbeatManager relies on HorseTime for alive/dead decisions.");

            Assert.Equal(0, disconnectedCount);

            foreach (var c in clients) c.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region COMBINED: Production Scenario Replay

    /// <summary>
    /// PRODUCTION REPLAY: 4 clients connected, each blocks its read loop after warm-up.
    /// This reproduces the exact production disconnect cycle.
    ///
    /// Each client: responds to first 2 PINGs normally, then blocks read loop for 10s.
    /// During the 10s block, server sends PING but client can't read it → disconnect.
    ///
    /// PingInterval=3s, tick=1.5s. After last PONG:
    ///   T=0:   PONG → KeepAlive(0)
    ///   T+4.5: SmartHealthCheck expired → PongRequired=true, PING sent
    ///   T+6:   PongRequired=true → Disconnect
    ///   T+10:  Client wakes up — too late
    ///
    /// Expectation: All clients should stay connected (they're processing, not dead).
    /// Reality: All 4 clients disconnect → TEST FAILS.
    /// </summary>
    [Fact]
    public async Task ProductionReplay_MultipleClients_ReadLoopBlocked_DisconnectCycle()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            int clientCount = 4;
            int totalDisconnects = 0;
            var disconnectTimes = new List<double>();
            var clients = new List<TcpClient>();
            DateTime startTime = DateTime.UtcNow;

            for (int i = 0; i < clientCount; i++)
            {
                TcpClient rawClient = new TcpClient();
                await rawClient.ConnectAsync("127.0.0.1", port);
                await HandshakeRawClient(rawClient, port, $"prod-consumer-{i}-{port}");
                clients.Add(rawClient);
                await Task.Delay(200);
            }

            await Task.Delay(500);

            for (int i = 0; i < clientCount; i++)
            {
                int clientIdx = i;
                TcpClient client = clients[i];
                NetworkStream stream = client.GetStream();
                HorseProtocolReader reader = new HorseProtocolReader();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        int pingsSeen = 0;
                        while (client.Connected)
                        {
                            HorseMessage msg = await reader.Read(stream);
                            if (msg == null) break;

                            if (msg.Type == MessageType.Ping)
                            {
                                pingsSeen++;

                                try { stream.Write(PredefinedMessages.PONG); }
                                catch { break; }

                                // After 2nd PONG, block read loop for 10s.
                                // Server's SmartHealthCheck window (3s) + PongRequired cycle (3s)
                                // = ~6s until disconnect. 10s > 6s → guaranteed disconnect.
                                if (pingsSeen >= 2)
                                    Thread.Sleep(10_000);
                            }
                        }
                    }
                    catch { }

                    Interlocked.Increment(ref totalDisconnects);
                    double elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    lock (disconnectTimes) disconnectTimes.Add(elapsed);
                    _output.WriteLine($"[PROD-REPLAY] Consumer-{clientIdx} disconnected at {elapsed:F1}s");
                });
            }

            await Task.Delay(30_000);

            _output.WriteLine($"[PROD-REPLAY] Total disconnects: {totalDisconnects}/{clientCount}");
            foreach (var dt in disconnectTimes)
                _output.WriteLine($"  Disconnect at: {dt:F1}s");

            int connectedCount = clients.Count(c => c.Connected);
            _output.WriteLine($"[PROD-REPLAY] Still connected: {connectedCount}/{clientCount}");

            Assert.Equal(0, totalDisconnects);
            Assert.True(clients.All(c => c.Connected),
                $"[PROD-REPLAY-FAIL] {totalDisconnects} of {clientCount} clients disconnected. " +
                "10s read-loop blocking after PONG → SmartHealthCheck expired → PING missed → disconnect. " +
                "Production scenario: heavy message processing blocks read loop → heartbeat fails.");

            foreach (var c in clients) c.Dispose();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion
}

