using System.Net.Sockets;
using System.Threading;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Network;

/// <summary>
/// Tests that reproduce and diagnose the production issue where an idle producer
/// (a client that connects but sends no messages) gets disconnected unexpectedly.
///
/// Production symptoms observed (Crow server):
///   - Client "crow_play_ground_producer" connects at 21:45:41
///   - Disconnected at 21:45:50 (only 9 seconds later!)
///   - Server logs: System.IO.IOException: Unable to read data from the transport connection: Operation canceled
///                  ---> System.Net.Sockets.SocketException (89): Operation canceled
///                  at HorseProtocolReader.ReadCertainBytes(...)
///                  at HorseProtocol.HandleConnection(...)
///                  at ConnectionHandler.AcceptClient(...)
///   - Client reconnects at 21:47:40, disconnected again at 21:48:22 (42 seconds later)
///
/// Key observations from the code:
///   - HorseClient.SmartHealthCheck defaults to FALSE
///   - SocketBase.SmartHealthCheck defaults to TRUE
///   - In HorseSocket.Start(): OnConnected() fires first (timer starts with SmartHealthCheck=true),
///     then _client.OnConnected() overrides SmartHealthCheck to false
///   - ClientSocketBase._pingIntervalTicks defaults to 60 seconds, PingInterval property updates it
///     but the property is set AFTER OnConnected()/CreatePingTimer()
///   - ConnectionHandler.AcceptClient receives the server's CancellationToken which flows to
///     initial ReadAsync — if KeepAliveManager kills the ConnectionInfo, the socket is closed
///     causing SocketException (89): Operation canceled
/// </summary>
public class IdleProducerDisconnectTest
{
    #region Helpers

    private static async Task<bool> WaitUntil(Func<bool> predicate, int timeoutMs = 10000, int pollMs = 100)
    {
        int elapsed = 0;
        while (elapsed < timeoutMs)
        {
            if (predicate())
                return true;

            await Task.Delay(pollMs);
            elapsed += pollMs;
        }

        return predicate();
    }

    #endregion

    #region Core: Idle Producer Must Stay Connected

    /// <summary>
    /// The exact production scenario: a producer connects and sits idle (sends no messages).
    /// Uses default HorseClient settings (PingInterval=15s, SmartHealthCheck=false).
    /// Server PingInterval=3s (common test default).
    /// The connection MUST survive at least 60 seconds of idle time.
    /// 
    /// Production bug: client was disconnected after only 9 seconds.
    /// </summary>
    [Fact]
    public async Task IdleProducer_DefaultSettings_StaysConnected60s()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SetClientName("idle_producer_test");
            // Default: PingInterval=15s, SmartHealthCheck=false — same as production

            bool disconnected = false;
            double? disconnectAfterSec = null;
            DateTime connectTime = DateTime.UtcNow;

            client.Disconnected += _ =>
            {
                disconnected = true;
                disconnectAfterSec = (DateTime.UtcNow - connectTime).TotalSeconds;
            };

            connectTime = DateTime.UtcNow;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected, "Client should connect successfully");

            // Production bug manifested at 9 seconds — wait 60s to be thorough
            await Task.Delay(60_000);

            Assert.True(client.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"PRODUCTION BUG: Idle producer disconnected after {disconnectAfterSec.Value:F1}s"
                    : "Idle producer should remain connected for 60s");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Parametric test: various server PingInterval vs client PingInterval combinations.
    /// Covers cases where server pings much faster than client, and vice versa.
    /// All combinations should keep the idle producer alive.
    /// </summary>
    [Theory]
    [InlineData(1, 15)]   // Very aggressive server, lazy client (closest to production bug)
    [InlineData(3, 15)]   // Common test default
    [InlineData(5, 30)]   // Medium server, very lazy client
    [InlineData(3, 60)]   // Server aggressive, client barely pings
    [InlineData(10, 10)]  // Symmetric
    [InlineData(120, 15)] // Default server (2 min), default client (15s)
    public async Task IdleProducer_PingIntervalCombinations_StaysAlive(
        int serverPingSec, int clientPingSec)
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: serverPingSec, requestTimeout: 120);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(clientPingSec);
            client.SetClientName($"idle-sv{serverPingSec}-cl{clientPingSec}");

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            // Wait at least 3x the larger ping interval, capped at 30s for test speed
            int waitMs = Math.Min(Math.Max(serverPingSec, clientPingSec) * 3 * 1000, 30_000);
            waitMs = Math.Max(waitMs, 10_000); // minimum 10s

            await Task.Delay(waitMs);

            Assert.True(client.IsConnected,
                $"Idle producer (server ping={serverPingSec}s, client ping={clientPingSec}s) " +
                $"must survive {waitMs / 1000}s idle");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region SmartHealthCheck Mismatch

    /// <summary>
    /// HorseClient.SmartHealthCheck defaults to FALSE.
    /// SocketBase.SmartHealthCheck defaults to TRUE.
    ///
    /// In HorseSocket.Start():
    ///   1. OnConnected() → CreatePingTimer() — timer starts with SmartHealthCheck=TRUE (SocketBase default)
    ///   2. _client.OnConnected() → _socket.SmartHealthCheck = FALSE (HorseClient default)
    ///
    /// The timer starts checking with SmartHealthCheck=true logic:
    ///   if (HorseTime.ServerTicks - LastAliveTimeTicks > _pingIntervalTicks) → send ping
    /// where _pingIntervalTicks defaults to 60s (not the HorseClient.PingInterval of 15s).
    ///
    /// Then _client.OnConnected() sets SmartHealthCheck=false and PingInterval=15s.
    /// The timer switches to: if (DateTime.UtcNow - _lastPingSent > PingInterval)
    ///
    /// This test ensures the mismatch window doesn't cause disconnection.
    /// </summary>
    [Fact]
    public async Task SmartHealthCheckMismatch_DefaultFalse_StaysConnected()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            // SmartHealthCheck defaults to true (matching server default). Override to false for this test.
            Assert.True(client.SmartHealthCheck, "HorseClient.SmartHealthCheck should default to true");
            client.SmartHealthCheck = false;
            Assert.Equal(TimeSpan.FromSeconds(15), client.PingInterval);

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            // Server PingInterval=2s, tickInterval=1s → many heartbeat cycles in 20s
            await Task.Delay(20_000);

            Assert.True(client.IsConnected,
                "SmartHealthCheck=false client should stay connected via auto PONG responses to server PINGs");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// SmartHealthCheck=true on both client and server.
    /// Idle producer sends no messages, so SmartHealthCheck won't suppress pings.
    /// Server must still send PINGs, client must respond with PONGs.
    /// </summary>
    [Fact]
    public async Task SmartHealthCheck_Enabled_IdleProducer_StaysConnected()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SmartHealthCheck = true;
            client.PingInterval = TimeSpan.FromSeconds(15);

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            await Task.Delay(20_000);

            Assert.True(client.IsConnected,
                "SmartHealthCheck=true idle producer should survive via server pings");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region Intermittent Traffic Patterns

    /// <summary>
    /// Producer sends a message every 10 seconds — between sends it is idle.
    /// Server PingInterval=3s means the server will ping during idle gaps.
    /// </summary>
    [Fact]
    public async Task IntermittentProducer_SendsEvery10s_StaysAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SetClientName("intermittent_producer");

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            // Send a message every 10 seconds for 60 seconds
            for (int i = 0; i < 6; i++)
            {
                await Task.Delay(10_000);

                if (!client.IsConnected)
                    break;

                HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "test-idle-queue");
                msg.SetStringContent($"msg-{i}");
                await client.SendAsync(msg, CancellationToken.None);
            }

            Assert.True(client.IsConnected,
                "Intermittent producer should stay connected across idle-send-idle cycles");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Producer sends a burst of 20 messages, then goes completely idle for 30 seconds.
    /// The idle period after the burst must be survived via heartbeat.
    /// </summary>
    [Fact]
    public async Task BurstThenIdle30s_StaysAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SetClientName("burst_then_idle_producer");

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            // Burst: 20 messages rapidly
            for (int i = 0; i < 20; i++)
            {
                HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "test-burst-queue");
                msg.SetStringContent($"burst-{i}");
                await client.SendAsync(msg, CancellationToken.None);
                await Task.Delay(50);
            }

            // Then complete idle for 30 seconds
            await Task.Delay(30_000);

            Assert.True(client.IsConnected,
                "Producer must survive 30s idle after burst — server pings keep it alive");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region Reconnection Stability

    /// <summary>
    /// Production pattern: connect → disconnect → reconnect → disconnect again.
    /// After reconnection, the new session must be stable.
    /// Production showed: connect → 9s disconnect → reconnect → 42s disconnect.
    /// </summary>
    [Fact]
    public async Task Reconnect_AfterServerRestart_SecondSessionStable()
    {
        // --- First session ---
        TestHorseRider server1 = new TestHorseRider();
        await server1.Initialize();
        int port1 = server1.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port1 > 0);

        HorseClient client1 = new HorseClient();
        client1.SetClientName("reconnect_test_producer");

        bool disconnected1 = false;
        client1.Disconnected += _ => disconnected1 = true;
        await client1.ConnectAsync($"horse://localhost:{port1}");
        await Task.Delay(500);
        Assert.True(client1.IsConnected);

        // Kill the server
        server1.Stop();
        await WaitUntil(() => disconnected1, 15_000);
        client1.Disconnect();

        // --- Second session (reconnect to fresh server) ---
        TestHorseRider server2 = new TestHorseRider();
        await server2.Initialize();
        int port2 = server2.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port2 > 0);

        try
        {
            HorseClient client2 = new HorseClient();
            client2.SetClientName("reconnected_producer");

            bool disconnected2 = false;
            double? disconnectAfterSec = null;
            DateTime connectTime = DateTime.UtcNow;
            client2.Disconnected += _ =>
            {
                disconnected2 = true;
                disconnectAfterSec = (DateTime.UtcNow - connectTime).TotalSeconds;
            };

            connectTime = DateTime.UtcNow;
            await client2.ConnectAsync($"horse://localhost:{port2}");
            await Task.Delay(500);
            Assert.True(client2.IsConnected);

            // Must stay alive for 45 seconds (production showed 42s disconnect on 2nd session)
            await Task.Delay(45_000);

            Assert.True(client2.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"Second session disconnected after {disconnectAfterSec.Value:F1}s — matches production bug pattern"
                    : "Reconnected producer must remain stable");
            Assert.False(disconnected2);

            client2.Disconnect();
        }
        finally
        {
            server2.Stop();
        }
    }

    #endregion

    #region Multiple Idle Producers

    /// <summary>
    /// 10 idle producers connected simultaneously.
    /// HeartbeatManager iterates all clients — none should be starved or missed.
    /// </summary>
    [Fact]
    public async Task TenIdleProducers_AllSurvive30s()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            var clients = new List<HorseClient>();
            var disconnectedFlags = new bool[10];

            for (int i = 0; i < 10; i++)
            {
                HorseClient c = new HorseClient();
                c.SetClientName($"idle_producer_{i}");
                int idx = i;
                c.Disconnected += _ => disconnectedFlags[idx] = true;
                await c.ConnectAsync($"horse://localhost:{port}");
                clients.Add(c);
            }

            await Task.Delay(500);
            for (int i = 0; i < clients.Count; i++)
                Assert.True(clients[i].IsConnected, $"Client {i} should connect");

            // All idle for 30 seconds
            await Task.Delay(30_000);

            for (int i = 0; i < clients.Count; i++)
            {
                Assert.True(clients[i].IsConnected, $"Idle producer {i} must survive 30s");
                Assert.False(disconnectedFlags[i], $"No disconnect for producer {i}");
            }

            foreach (var c in clients)
                c.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region Aggressive Server PingInterval

    /// <summary>
    /// Most aggressive server setting: PingInterval=1s (tickInterval=1s clamped).
    /// Idle producer with default client settings (PingInterval=15s).
    /// This is the scenario most likely to trigger the production bug.
    /// </summary>
    [Fact]
    public async Task AggressiveServerPing1s_IdleProducer_Survives30s()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 1, requestTimeout: 10);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SetClientName("aggressive_ping_producer");
            // Default: PingInterval=15s, SmartHealthCheck=false

            bool disconnected = false;
            double? disconnectAfterSec = null;
            DateTime connectTime = DateTime.UtcNow;
            client.Disconnected += _ =>
            {
                disconnected = true;
                disconnectAfterSec = (DateTime.UtcNow - connectTime).TotalSeconds;
            };

            connectTime = DateTime.UtcNow;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            // 30 heartbeat cycles with 1s ping
            await Task.Delay(30_000);

            Assert.True(client.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"Idle producer disconnected after {disconnectAfterSec.Value:F1}s with PingInterval=1s — " +
                      "potential production bug!"
                    : "Idle producer must survive aggressive server pinging (1s)");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region RequestTimeout Interaction

    /// <summary>
    /// Tests that RequestTimeout does NOT affect already-established piped connections.
    /// 
    /// ConnectionHandler sets MaxAlive = now + RequestTimeout on new connections.
    /// KeepAliveManager may close connections where MaxAlive has passed.
    /// But once connection state transitions to "Pipe" (after protocol handshake),
    /// the connection should be managed by HeartbeatManager, not KeepAliveManager.
    /// 
    /// If this interaction is broken, connections could be killed by RequestTimeout
    /// even after successful handshake — this would explain the 9-second disconnect.
    /// </summary>
    [Theory]
    [InlineData(5)]   // Very short request timeout
    [InlineData(10)]  // Short
    [InlineData(30)]  // Normal
    public async Task RequestTimeout_DoesNotKillEstablishedConnection(int requestTimeoutSec)
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: requestTimeoutSec);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SetClientName("request_timeout_test");

            bool disconnected = false;
            double? disconnectAfterSec = null;
            DateTime connectTime = DateTime.UtcNow;
            client.Disconnected += _ =>
            {
                disconnected = true;
                disconnectAfterSec = (DateTime.UtcNow - connectTime).TotalSeconds;
            };

            connectTime = DateTime.UtcNow;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            // Wait longer than the requestTimeout — connection must NOT be killed
            int waitMs = (requestTimeoutSec + 10) * 1000;
            await Task.Delay(waitMs);

            Assert.True(client.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"Connection killed after {disconnectAfterSec.Value:F1}s — " +
                      $"RequestTimeout={requestTimeoutSec}s may be killing established connections!"
                    : $"Established connection must survive beyond RequestTimeout={requestTimeoutSec}s");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region Diagnostic: Timing Analysis

    /// <summary>
    /// Diagnostic test that tracks connection/disconnection events with precise timing.
    /// If it fails, the error message reveals the exact timing pattern to compare
    /// against the production pattern: Connected@0s → Disconnected@9s → Reconnect → Disconnected@42s.
    /// </summary>
    [Fact]
    public async Task Diagnostic_TrackDisconnectTiming()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 10);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SetClientName("timing_diagnostic");

            DateTime connectTime = DateTime.UtcNow;
            var events = new List<(string Event, double Seconds)>();

            client.Connected += _ =>
            {
                lock (events)
                    events.Add(("Connected", (DateTime.UtcNow - connectTime).TotalSeconds));
            };

            client.Disconnected += _ =>
            {
                lock (events)
                    events.Add(("Disconnected", (DateTime.UtcNow - connectTime).TotalSeconds));
            };

            connectTime = DateTime.UtcNow;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            // Wait 45 seconds — enough to observe 9s or 42s patterns
            await Task.Delay(45_000);

            string timeline;
            lock (events)
                timeline = string.Join(" → ", events.Select(e => $"{e.Event}@{e.Seconds:F1}s"));

            Assert.True(client.IsConnected,
                $"Idle producer disconnected! Timeline: [{timeline}]. " +
                "Production pattern: Connected@0s → Disconnected@9s");

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Exact production match: client named "crow_play_ground_producer" with producer type.
    /// Various idle durations to catch both the 9s and 42s disconnect patterns.
    /// </summary>
    [Theory]
    [InlineData(15)]   // Production first disconnect was at 9s — should survive 15s
    [InlineData(45)]   // Production second disconnect was at 42s — should survive 45s
    [InlineData(60)]   // Extended stability check
    public async Task ProductionExactMatch_CrowPlaygroundProducer(int idleSeconds)
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 5, requestTimeout: 30);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SetClientName("crow_play_ground_producer");
            client.SetClientType("producer");

            bool disconnected = false;
            double? disconnectAfterSec = null;
            DateTime connectTime = DateTime.UtcNow;
            client.Disconnected += _ =>
            {
                disconnected = true;
                disconnectAfterSec = (DateTime.UtcNow - connectTime).TotalSeconds;
            };

            connectTime = DateTime.UtcNow;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            await Task.Delay(idleSeconds * 1000);

            Assert.True(client.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"crow_play_ground_producer disconnected after {disconnectAfterSec.Value:F1}s " +
                      $"(expected >{idleSeconds}s). PRODUCTION BUG REPRODUCED!"
                    : $"crow_play_ground_producer survived {idleSeconds}s idle ✓");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region HorseClientBuilder: Default Configuration Stability

    /// <summary>
    /// Uses HorseClientBuilder (the common DI/production setup path) to create a client.
    /// Ensures builder-created clients also stay alive when idle.
    /// </summary>
    [Fact]
    public async Task HorseClientBuilder_DefaultConfig_IdleProducerStaysAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClientBuilder builder = new HorseClientBuilder();
            builder.AddHost($"horse://localhost:{port}");
            builder.SetClientName("builder_idle_producer");

            HorseClient client = builder.Build();

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync();
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            await Task.Delay(30_000);

            Assert.True(client.IsConnected,
                "Builder-created idle producer should stay connected 30s");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion
}

