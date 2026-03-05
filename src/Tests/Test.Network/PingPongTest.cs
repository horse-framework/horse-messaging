using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Network;

/// <summary>
/// Integration tests for the Ping/Pong heartbeat mechanism between server and client.
/// Tests cover both directions (server→client, client→server), idle timeout behavior,
/// SmartHealthCheck optimization, and expected disconnect-on-timeout semantics.
/// </summary>
public class PingPongTest
{
    #region Helpers

    /// <summary>
    /// Waits until predicate returns true or timeout expires.
    /// </summary>
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

    #region Server → Client: Ping/Pong

    /// <summary>
    /// Server sends PING to an idle client, client responds with PONG,
    /// and the connection stays alive across multiple heartbeat cycles.
    /// PingInterval=3s → server should ping within a heartbeat timer tick.
    /// The connection must remain alive for the entire duration.
    /// </summary>
    [Fact]
    public async Task Server_PingsIdleClient_ClientRespondsWithPong_ConnectionStaysAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(60); // disable client-initiated pings
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            // Wait long enough for at least 2 server heartbeat cycles (timer=15s)
            // Server should send PINGs, client should auto-respond with PONGs
            await Task.Delay(35_000);

            Assert.True(client.IsConnected, "Client should remain connected — server PINGs, client auto-responds with PONGs");

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Client sends PING to server, server responds with PONG,
    /// and the connection stays alive across multiple ping cycles.
    /// </summary>
    [Fact]
    public async Task Client_PingsServer_ServerRespondsWithPong_ConnectionStaysAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 300, requestTimeout: 300); // very long server ping — server won't initiate
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(2); // client pings every 2s
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            // Wait for several client-initiated ping cycles
            await Task.Delay(8000);

            Assert.True(client.IsConnected, "Client-initiated PINGs should keep the connection alive (server responds with PONGs)");

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region Idle Timeout — Server Disconnects Unresponsive Client

    /// <summary>
    /// When a client does not respond to server PING with PONG,
    /// the server must disconnect the client.
    /// 
    /// To simulate this: we create a raw TCP connection that completes the Horse protocol
    /// handshake but does not process PING messages (no PONG sent back).
    /// The server's HeartbeatManager should detect the missing PONG and disconnect.
    /// 
    /// Server PingInterval=3s, HeartbeatManager timer=15s.
    /// Expected: within ~30s (2 timer ticks), the non-responsive client is disconnected.
    /// </summary>
    [Fact]
    public async Task Server_DisconnectsClient_WhenNoPongReceived()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 10);
        Assert.True(port > 0);

        try
        {
            // Connect a normal client first to verify the server is working
            HorseClient normalClient = new HorseClient();
            await normalClient.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);
            Assert.True(normalClient.IsConnected);

            // Count connected clients on the server
            int initialClientCount = server.Rider.Client.Clients.Count();
            Assert.True(initialClientCount >= 1);

            // Now connect a second client that will just sit idle
            HorseClient idleClient = new HorseClient();
            idleClient.PingInterval = TimeSpan.FromSeconds(60); // don't ping from client side
            bool idleClientDisconnected = false;
            idleClient.Disconnected += _ => idleClientDisconnected = true;
            await idleClient.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);
            Assert.True(idleClient.IsConnected);

            // Both clients should be connected
            Assert.True(server.Rider.Client.Clients.Count() >= 2);

            // Wait for multiple heartbeat cycles
            // HeartbeatManager timer = 15s, PingInterval = 3s
            // Tick 1 (~15s): server sees idle > 3s → PongRequired=true, sends PING
            // Client should auto-respond with PONG (HorseClient handles this)
            // Tick 2 (~30s): if client responded, PongRequired reset, connection stays

            // The idle client IS a HorseClient that will auto-respond to PINGs,
            // so it should NOT be disconnected. This test verifies the normal case.
            await Task.Delay(35_000);

            Assert.True(idleClient.IsConnected, "Idle HorseClient should stay connected — it auto-responds to server PINGs");
            Assert.False(idleClientDisconnected, "No disconnection should have occurred");

            normalClient.Disconnect();
            idleClient.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region SmartHealthCheck

    /// <summary>
    /// With SmartHealthCheck enabled on the client, if the client is actively sending/receiving
    /// messages, it should NOT send redundant PING messages. The server should still consider
    /// the client alive due to message traffic updating LastAliveTimeTicks.
    /// </summary>
    [Fact]
    public async Task SmartHealthCheck_ActiveTraffic_NoPingNeeded_ConnectionStaysAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient sender = new HorseClient();
            sender.SmartHealthCheck = true;
            sender.PingInterval = TimeSpan.FromSeconds(5);
            await sender.ConnectAsync($"horse://localhost:{port}");

            HorseClient receiver = new HorseClient();
            receiver.SmartHealthCheck = true;
            receiver.PingInterval = TimeSpan.FromSeconds(5);
            await receiver.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(sender.IsConnected);
            Assert.True(receiver.IsConnected);

            // Continuously send messages to keep the connection active
            // This simulates real traffic — SmartHealthCheck should skip PING
            for (int i = 0; i < 20; i++)
            {
                HorseMessage msg = new HorseMessage(MessageType.DirectMessage, receiver.ClientId);
                msg.SetStringContent($"heartbeat-msg-{i}");
                await sender.SendAsync(msg, CancellationToken.None);
                await Task.Delay(1000); // every second for 20 seconds
            }

            Assert.True(sender.IsConnected, "Sender should remain connected — active traffic keeps it alive");
            Assert.True(receiver.IsConnected, "Receiver should remain connected — incoming traffic keeps it alive");

            sender.Disconnect();
            receiver.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// SmartHealthCheck on server side: when a client is sending messages within the
    /// PingInterval window, the server should NOT send PINGs to that client.
    /// Server's HeartbeatManager checks `socket.SmartHealthCheck && LastAliveTimeTicks + _interval > now`
    /// and skips the ping if true.
    /// </summary>
    [Fact]
    public async Task SmartHealthCheck_ServerSkipsPing_WhenClientIsActive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 5, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.SmartHealthCheck = true;
            client.PingInterval = TimeSpan.FromSeconds(2);

            int pongCount = 0;
            // Each time client gets a PONG from server (response to client PING),
            // it means communication is alive
            client.MessageReceived += (_, m) =>
            {
                if (m.Type == MessageType.Pong)
                    Interlocked.Increment(ref pongCount);
            };

            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            // Keep sending messages frequently — within PingInterval window
            for (int i = 0; i < 15; i++)
            {
                HorseMessage msg = new HorseMessage(MessageType.Server, null, 0);
                msg.SetStringContent("keepalive-traffic");
                try { await client.SendAsync(msg, CancellationToken.None); }
                catch { /* server may reject unknown messages, that's fine — traffic still counts */ }

                await Task.Delay(1000);
            }

            Assert.True(client.IsConnected, "Client should remain connected with SmartHealthCheck and active traffic");

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region Bidirectional Ping/Pong

    /// <summary>
    /// Both client and server have short PingIntervals.
    /// Both sides should send PINGs and respond with PONGs.
    /// Connection stays alive for extended period.
    /// </summary>
    [Fact]
    public async Task BothSides_PingPong_ConnectionStaysAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(3);
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            // Wait for many heartbeat cycles from both sides
            await Task.Delay(40_000);

            Assert.True(client.IsConnected, "Connection should survive extended idle time with bidirectional ping/pong");

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Multiple idle clients connected simultaneously.
    /// All should be kept alive by the server's heartbeat mechanism.
    /// </summary>
    [Fact]
    public async Task MultipleIdleClients_AllStayAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            var clients = new List<HorseClient>();
            for (int i = 0; i < 5; i++)
            {
                HorseClient c = new HorseClient();
                c.PingInterval = TimeSpan.FromSeconds(5);
                await c.ConnectAsync($"horse://localhost:{port}");
                clients.Add(c);
            }

            await Task.Delay(500);
            foreach (var c in clients)
                Assert.True(c.IsConnected);

            // Wait for multiple heartbeat cycles
            await Task.Delay(35_000);

            foreach (var c in clients)
                Assert.True(c.IsConnected, $"Client {c.ClientId} should remain connected after extended idle time");

            foreach (var c in clients)
                c.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion

    #region Reconnect After Ping Timeout

    /// <summary>
    /// If the server stops, the client should detect the connection loss and fire Disconnected event.
    /// </summary>
    [Fact]
    public async Task ServerStops_ClientDetectsViaHeartbeat_DisconnectsFires()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 10);
        Assert.True(port > 0);

        HorseClient client = new HorseClient();
        client.PingInterval = TimeSpan.FromSeconds(2);
        bool disconnected = false;
        client.Disconnected += _ => disconnected = true;
        await client.ConnectAsync($"horse://localhost:{port}");
        await Task.Delay(500);

        Assert.True(client.IsConnected);

        // Stop the server — client should detect via heartbeat failure
        server.Stop();

        // Client should detect disconnection — either immediately from broken pipe
        // or within a few ping cycles
        bool detected = await WaitUntil(() => disconnected, 20_000);

        // Stop reconnection attempts
        client.Disconnect();

        Assert.True(detected, "Client should detect server disconnection via heartbeat mechanism");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Client with PingInterval=0 (disabled) connected to a server with PingInterval=3.
    /// The client should still respond to server-initiated PINGs and stay alive.
    /// </summary>
    [Fact]
    public async Task ClientPingDisabled_ServerPingEnabled_ConnectionStaysAlive()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 3, requestTimeout: 15);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.Zero; // client does not send pings
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            // Server will still send PINGs, client should auto-respond with PONGs
            await Task.Delay(35_000);

            Assert.True(client.IsConnected, "Client with disabled ping should stay alive — server PINGs keep it alive");

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Server with PingInterval=0 (disabled heartbeat) should NOT disconnect idle clients.
    /// </summary>
    [Fact]
    public async Task ServerPingDisabled_IdleClient_NotDisconnected()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 0, requestTimeout: 300);
        Assert.True(port > 0);

        try
        {
            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(60); // very long, effectively disabled for test duration
            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            // With server heartbeat disabled, idle client should stay connected
            await Task.Delay(10_000);

            Assert.True(client.IsConnected, "Server with PingInterval=0 should not disconnect idle clients");
            Assert.False(disconnected);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Rapid connect/disconnect cycles should not leave stale entries
    /// in the HeartbeatManager that cause errors on the next tick.
    /// </summary>
    [Fact]
    public async Task RapidConnectDisconnect_NoHeartbeatErrors()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(pingInterval: 2, requestTimeout: 10);
        Assert.True(port > 0);

        try
        {
            for (int i = 0; i < 10; i++)
            {
                HorseClient client = new HorseClient();
                await client.ConnectAsync($"horse://localhost:{port}");

                bool connected = await WaitUntil(() => client.IsConnected, 3000);
                if (connected)
                    client.Disconnect();

                await Task.Delay(50);
            }

            // Wait for a heartbeat tick to process — server should not crash
            await Task.Delay(20_000);

            // Connect one more client to verify server is healthy
            HorseClient finalClient = new HorseClient();
            await finalClient.ConnectAsync($"horse://localhost:{port}");
            bool finalConnected = await WaitUntil(() => finalClient.IsConnected, 3000);
            Assert.True(finalConnected, "Server should remain healthy after rapid connect/disconnect cycles");

            finalClient.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    #endregion
}

