using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

        byte[] protocolResponse = new byte[PredefinedMessages.PROTOCOL_BYTES_V4.Length];
        int totalRead = 0;
        while (totalRead < protocolResponse.Length)
        {
            int r = await netStream.ReadAsync(protocolResponse, totalRead, protocolResponse.Length - totalRead);
            if (r == 0)
                break;
            totalRead += r;
        }

        return netStream;
    }

    #endregion

    #region ISSUE 1: Read Loop Blocking — PONG Delayed Under Message Processing Load

    [Fact]
    public async Task Issue1_ReadLoopBlocking_PongDelayedByMessageProcessing()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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
                        if (msg == null)
                        {
                            disconnected = true;
                            break;
                        }

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
                                Thread.Sleep(1500);
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                }
            });

            await Task.Delay(15_000);

            _output.WriteLine($"[ISSUE1] PINGs: {pingCount}, PONGs: {pongCount}, Disconnected: {disconnected}");

            Assert.False(disconnected,
                $"[ISSUE1-FAIL] Client disconnected because PONG was delayed by 1.5s (tick=1s). " +
                $"PINGs: {pingCount}, PONGs: {pongCount}. " +
                "A 1.5s PONG delay caused by message processing should be tolerable.");

            rawClient.Dispose();
        }, pingInterval: 2, requestTimeout: 30);
    }

    #endregion

    #region ISSUE 2: Single-Tick Pong Window Too Narrow

    [Fact]
    public async Task Issue2_SingleTickPongWindow_TooNarrow()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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
                        if (msg == null)
                        {
                            disconnected = true;
                            break;
                        }

                        if (msg.Type == MessageType.Ping)
                        {
                            Interlocked.Increment(ref pingReceived);

                            await Task.Delay(1500);

                            if (!disconnected && rawClient.Connected)
                            {
                                try
                                {
                                    stream.Write(PredefinedMessages.PONG);
                                    Interlocked.Increment(ref pongSent);
                                }
                                catch
                                {
                                    disconnected = true;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                }
            });

            await Task.Delay(12_000);

            _output.WriteLine($"[ISSUE2] PINGs: {pingReceived}, PONGs: {pongSent}, Disconnected: {disconnected}");

            Assert.False(disconnected,
                $"[ISSUE2-FAIL] Client disconnected even though PONG was always sent (with 1.5s delay). " +
                $"PINGs: {pingReceived}, PONGs: {pongSent}. " +
                "1-tick pong window is too narrow for even minor processing delays.");

            rawClient.Dispose();
        }, pingInterval: 2, requestTimeout: 30);
    }

    #endregion

    #region ISSUE 3: Read Loop Completely Blocked — PING Never Read

    [Fact]
    public async Task Issue3_ReadLoopBlocked_NoPongDuringProcessing()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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

                            try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                            catch { disconnected = true; }

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

            Assert.False(disconnected,
                $"[ISSUE3-FAIL] Client disconnected {blockToDisconnect:F1}s after read loop was blocked. " +
                $"PINGs: {pingCount}, PONGs: {pongCount}. " +
                "Heartbeat is coupled to the read loop — blocking it for 10s causes disconnect.");

            rawClient.Dispose();
        }, pingInterval: 3, requestTimeout: 30);
    }

    #endregion

    #region ISSUE 4: Silent Write Failure — Server Correctly Disconnects Unresponsive Client

    [Fact]
    public async Task Issue4_SilentWriteFailure_ServerCorrectlyDisconnects()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                            else
                            {
                                int fails = Interlocked.Increment(ref silentFailCount);
                                if (fails == 1)
                                    firstSilentFail = DateTime.UtcNow;
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

            Assert.True(disconnected,
                $"[ISSUE4-FAIL] Server did NOT disconnect client after {silentFailCount} missed PONGs. " +
                "When PONG is silently lost, the server's heartbeat must detect and disconnect the client.");

            Assert.True(timeToDisconnect > 0 && timeToDisconnect < 15,
                $"[ISSUE4-FAIL] Disconnect took {timeToDisconnect:F1}s — expected within 15s grace window.");

            rawClient.Dispose();
        }, pingInterval: 2, requestTimeout: 30);
    }

    #endregion

    #region ISSUE 5: Write Contention — PONG Delayed By Stream Lock

    [Fact]
    public async Task Issue5_WriteContention_PongDelayedBehindLargeWrites()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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
                        if (msg == null)
                        {
                            disconnected = true;
                            break;
                        }

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
                                Thread.Sleep(1500);
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                }
            });

            await Task.Delay(15_000);

            _output.WriteLine($"[ISSUE5] PINGs: {pingCount}, PONGs: {pongCount}, Disconnected: {disconnected}");

            Assert.False(disconnected,
                $"[ISSUE5-FAIL] Client disconnected because PONG was delayed 1.5s by write contention. " +
                $"PINGs: {pingCount}, PONGs: {pongCount}. " +
                "Stream write serialization blocks PONG behind large data writes. " +
                "PONG has no priority mechanism — it waits in the FIFO write queue.");

            rawClient.Dispose();
        }, pingInterval: 2, requestTimeout: 30);
    }

    #endregion

    #region ISSUE 1+2 Combined: Processing Delay Exceeds PING Interval

    [Fact]
    public async Task Issue1_2_ProgressiveLoadIncrease_PongDelayExceedsPingInterval()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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
                        if (msg == null)
                        {
                            disconnected = true;
                            break;
                        }

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
                                Thread.Sleep(3500);
                                try { netStream.Write(PredefinedMessages.PONG); Interlocked.Increment(ref pongCount); }
                                catch { disconnected = true; }
                            }
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                }
            });

            await Task.Delay(20_000);

            _output.WriteLine($"[ISSUE1+2] PINGs: {pingCount}, PONGs: {pongCount}, Disconnected: {disconnected}");

            Assert.False(disconnected,
                $"[ISSUE1+2-FAIL] Client disconnected as processing time (3.5s) exceeded PING interval (2s). " +
                $"PINGs: {pingCount}, PONGs: {pongCount}. " +
                "No data sent during 3.5s window → SmartHealthCheck expired → disconnect.");

            rawClient.Dispose();
        }, pingInterval: 2, requestTimeout: 30);
    }

    #endregion

    #region ISSUE 6: HorseTime Drift Under Load

    [Fact]
    public async Task Issue6_HorseTimeDrift_CausesIncorrectHeartbeatDecisions()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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
            foreach (var c in clients)
                Assert.True(c.IsConnected);

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

            foreach (var c in clients)
                c.Disconnect();
        }, pingInterval: 2, requestTimeout: 30);
    }

    #endregion

    #region COMBINED: Production Scenario Replay

    [Fact]
    public async Task ProductionReplay_MultipleClients_ReadLoopBlocked_DisconnectCycle()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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
                            if (msg == null)
                                break;

                            if (msg.Type == MessageType.Ping)
                            {
                                pingsSeen++;

                                try { stream.Write(PredefinedMessages.PONG); }
                                catch { break; }

                                if (pingsSeen >= 2)
                                    Thread.Sleep(10_000);
                            }
                        }
                    }
                    catch
                    {
                    }

                    Interlocked.Increment(ref totalDisconnects);
                    double elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    lock (disconnectTimes)
                        disconnectTimes.Add(elapsed);
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

            foreach (var c in clients)
                c.Dispose();
        }, pingInterval: 3, requestTimeout: 30);
    }

    #endregion
}
