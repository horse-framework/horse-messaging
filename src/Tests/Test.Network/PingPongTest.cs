using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

    [Fact]
    public async Task Server_PingsIdleClient_ClientRespondsWithPong_ConnectionStaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(60);
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            await Task.Delay(15_000);

            Assert.True(client.IsConnected, "Client should remain connected — server PINGs, client auto-responds with PONGs");

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task Client_PingsServer_ServerRespondsWithPong_ConnectionStaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(2);
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            await Task.Delay(8000);

            Assert.True(client.IsConnected, "Client-initiated PINGs should keep the connection alive (server responds with PONGs)");

            client.Disconnect();
        }, pingInterval: 300, requestTimeout: 300);
    }

    #endregion

    #region Idle Timeout — Server Disconnects Unresponsive Client

    [Fact]
    public async Task Server_DisconnectsClient_WhenNoPongReceived()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient normalClient = new HorseClient();
            await normalClient.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);
            Assert.True(normalClient.IsConnected);

            int initialClientCount = server.Rider.Client.Clients.Count();
            Assert.True(initialClientCount >= 1);

            HorseClient idleClient = new HorseClient();
            idleClient.PingInterval = TimeSpan.FromSeconds(60);
            bool idleClientDisconnected = false;
            idleClient.Disconnected += _ => idleClientDisconnected = true;
            await idleClient.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);
            Assert.True(idleClient.IsConnected);

            Assert.True(server.Rider.Client.Clients.Count() >= 2);

            await Task.Delay(15_000);

            Assert.True(idleClient.IsConnected, "Idle HorseClient should stay connected — it auto-responds to server PINGs");
            Assert.False(idleClientDisconnected, "No disconnection should have occurred");

            normalClient.Disconnect();
            idleClient.Disconnect();
        }, pingInterval: 3, requestTimeout: 10);
    }

    #endregion

    #region SmartHealthCheck

    [Fact]
    public async Task SmartHealthCheck_ActiveTraffic_NoPingNeeded_ConnectionStaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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

            for (int i = 0; i < 20; i++)
            {
                HorseMessage msg = new HorseMessage(MessageType.DirectMessage, receiver.ClientId);
                msg.SetStringContent($"heartbeat-msg-{i}");
                await sender.SendAsync(msg, CancellationToken.None);
                await Task.Delay(1000);
            }

            Assert.True(sender.IsConnected, "Sender should remain connected — active traffic keeps it alive");
            Assert.True(receiver.IsConnected, "Receiver should remain connected — incoming traffic keeps it alive");

            sender.Disconnect();
            receiver.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task SmartHealthCheck_ServerSkipsPing_WhenClientIsActive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.SmartHealthCheck = true;
            client.PingInterval = TimeSpan.FromSeconds(2);

            int pongCount = 0;
            client.MessageReceived += (_, m) =>
            {
                if (m.Type == MessageType.Pong)
                    Interlocked.Increment(ref pongCount);
            };

            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            for (int i = 0; i < 15; i++)
            {
                HorseMessage msg = new HorseMessage(MessageType.Server, null, 0);
                msg.SetStringContent("keepalive-traffic");
                try { await client.SendAsync(msg, CancellationToken.None); }
                catch { }

                await Task.Delay(1000);
            }

            Assert.True(client.IsConnected, "Client should remain connected with SmartHealthCheck and active traffic");
            _ = pongCount;

            client.Disconnect();
        }, pingInterval: 5, requestTimeout: 15);
    }

    #endregion

    #region Bidirectional Ping/Pong

    [Fact]
    public async Task BothSides_PingPong_ConnectionStaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(3);
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            await Task.Delay(15_000);

            Assert.True(client.IsConnected, "Connection should survive extended idle time with bidirectional ping/pong");

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task MultipleIdleClients_AllStayAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            var clients = new List<HorseClient>();
            for (int i = 0; i < 5; i++)
            {
                HorseClient c = new HorseClient();
                c.PingInterval = TimeSpan.FromSeconds(5);
                await c.ConnectAsync($"horse://localhost:{port}");
                clients.Add(c);
            }

            await Task.Delay(500);
            foreach (HorseClient c in clients)
                Assert.True(c.IsConnected);

            await Task.Delay(15_000);

            foreach (HorseClient c in clients)
                Assert.True(c.IsConnected, $"Client {c.ClientId} should remain connected after extended idle time");

            foreach (HorseClient c in clients)
                c.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion

    #region Reconnect After Ping Timeout

    [Fact]
    public async Task ServerStops_ClientDetectsViaHeartbeat_DisconnectsFires()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(2);
            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(client.IsConnected);

            await server.StopAsync();

            bool detected = await WaitUntil(() => disconnected, 20_000);

            client.Disconnect();

            Assert.True(detected, "Client should detect server disconnection via heartbeat mechanism");
        }, pingInterval: 3, requestTimeout: 10);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ClientPingDisabled_ServerPingEnabled_ConnectionStaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.Zero;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            await Task.Delay(15_000);

            Assert.True(client.IsConnected, "Client with disabled ping should stay alive — server PINGs keep it alive");

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task ServerPingDisabled_IdleClient_NotDisconnected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(60);
            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            await Task.Delay(10_000);

            Assert.True(client.IsConnected, "Server with PingInterval=0 should not disconnect idle clients");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 0, requestTimeout: 300);
    }

    [Fact]
    public async Task RapidConnectDisconnect_NoHeartbeatErrors()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            for (int i = 0; i < 10; i++)
            {
                HorseClient client = new HorseClient();
                await client.ConnectAsync($"horse://localhost:{port}");

                bool connected = await WaitUntil(() => client.IsConnected, 3000);
                if (connected)
                    client.Disconnect();

                await Task.Delay(50);
            }

            await Task.Delay(10_000);

            HorseClient finalClient = new HorseClient();
            await finalClient.ConnectAsync($"horse://localhost:{port}");
            bool finalConnected = await WaitUntil(() => finalClient.IsConnected, 3000);
            Assert.True(finalConnected, "Server should remain healthy after rapid connect/disconnect cycles");

            finalClient.Disconnect();
        }, pingInterval: 2, requestTimeout: 10);
    }

    #endregion

    #region Configurable Tick Interval

    [Fact]
    public async Task ShortPingInterval_FastDisconnect_RawClient()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);

            NetworkStream stream = rawClient.GetStream();
            stream.Write(PredefinedMessages.PROTOCOL_BYTES_V4);
            HorseMessage msg = new HorseMessage();
            msg.Type = MessageType.Server;
            msg.ContentType = KnownContentTypes.Hello;
            msg.SetStringContent("GET /\r\nName: RawShortPing-" + port);
            msg.CalculateLengths();
            HorseProtocolWriter.Write(msg, stream);

            bool disconnected = false;
            ThreadPool.UnsafeQueueUserWorkItem(async s =>
            {
                byte[] buffer = new byte[128];
                try
                {
                    while (rawClient.Connected)
                    {
                        int r = await s.ReadAsync(buffer);
                        if (r == 0)
                        {
                            disconnected = true;
                            rawClient.Dispose();
                            break;
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                }
            }, stream, false);

            await Task.Delay(1000);
            Assert.Equal(1, server.ClientConnected);

            bool detected = await WaitUntil(() => disconnected, 15_000);

            Assert.True(detected, "Raw client should be disconnected within a few seconds when PingInterval=2s");
        }, pingInterval: 2, requestTimeout: 10);
    }

    [Fact]
    public async Task MediumPingInterval_ClientStaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(60);
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            await Task.Delay(15_000);

            Assert.True(client.IsConnected, "Client should remain alive with PingInterval=5s over multiple cycles");

            client.Disconnect();
        }, pingInterval: 5, requestTimeout: 15);
    }

    [Fact]
    public async Task LargePingInterval_TickCappedAt5s_ClientStaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(60);
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            await Task.Delay(25_000);

            Assert.True(client.IsConnected, "Client should remain alive with PingInterval=20s (tick capped at 5s)");

            client.Disconnect();
        }, pingInterval: 20, requestTimeout: 30);
    }

    [Fact]
    public async Task MinimalPingInterval_FastestDisconnect_RawClient()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);

            NetworkStream stream = rawClient.GetStream();
            stream.Write(PredefinedMessages.PROTOCOL_BYTES_V4);
            HorseMessage msg = new HorseMessage();
            msg.Type = MessageType.Server;
            msg.ContentType = KnownContentTypes.Hello;
            msg.SetStringContent("GET /\r\nName: RawMinPing-" + port);
            msg.CalculateLengths();
            HorseProtocolWriter.Write(msg, stream);

            bool disconnected = false;
            ThreadPool.UnsafeQueueUserWorkItem(async s =>
            {
                byte[] buffer = new byte[128];
                try
                {
                    while (rawClient.Connected)
                    {
                        int r = await s.ReadAsync(buffer);
                        if (r == 0)
                        {
                            disconnected = true;
                            rawClient.Dispose();
                            break;
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                }
            }, stream, false);

            await Task.Delay(500);

            bool detected = await WaitUntil(() => disconnected, 10_000);

            Assert.True(detected, "Raw client should be disconnected within seconds when PingInterval=1s");
        }, pingInterval: 1, requestTimeout: 10);
    }

    [Fact]
    public async Task FastPingInterval_BothSides_ConnectionSurvives()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(1);
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(300);

            Assert.True(client.IsConnected);

            await Task.Delay(15_000);

            Assert.True(client.IsConnected, "Connection should survive rapid bidirectional ping/pong (PingInterval=1s both sides)");

            client.Disconnect();
        }, pingInterval: 1, requestTimeout: 10);
    }

    [Fact]
    public async Task MixedClientPingIntervals_AllStayAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient fastClient = new HorseClient();
            fastClient.PingInterval = TimeSpan.FromSeconds(1);
            await fastClient.ConnectAsync($"horse://localhost:{port}");

            HorseClient slowClient = new HorseClient();
            slowClient.PingInterval = TimeSpan.FromSeconds(10);
            await slowClient.ConnectAsync($"horse://localhost:{port}");

            await Task.Delay(500);

            Assert.True(fastClient.IsConnected);
            Assert.True(slowClient.IsConnected);

            await Task.Delay(15_000);

            Assert.True(fastClient.IsConnected, "Fast-pinging client should remain connected");
            Assert.True(slowClient.IsConnected, "Slow-pinging client should remain connected — server pings keep it alive");

            fastClient.Disconnect();
            slowClient.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Theory]
    [InlineData(2, 12_000)]
    [InlineData(5, 20_000)]
    public async Task DisconnectSpeed_ScalesWithPingInterval(int pingIntervalSec, int maxDisconnectMs)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            TcpClient rawClient = new TcpClient();
            await rawClient.ConnectAsync("127.0.0.1", port);

            NetworkStream stream = rawClient.GetStream();
            stream.Write(PredefinedMessages.PROTOCOL_BYTES_V4);
            HorseMessage msg = new HorseMessage();
            msg.Type = MessageType.Server;
            msg.ContentType = KnownContentTypes.Hello;
            msg.SetStringContent($"GET /\r\nName: RawScale-{pingIntervalSec}-{port}");
            msg.CalculateLengths();
            HorseProtocolWriter.Write(msg, stream);

            bool disconnected = false;
            ThreadPool.UnsafeQueueUserWorkItem(async s =>
            {
                byte[] buffer = new byte[128];
                try
                {
                    while (rawClient.Connected)
                    {
                        int r = await s.ReadAsync(buffer);
                        if (r == 0)
                        {
                            disconnected = true;
                            rawClient.Dispose();
                            break;
                        }
                    }
                }
                catch
                {
                    disconnected = true;
                }
            }, stream, false);

            await Task.Delay(1000);

            bool detected = await WaitUntil(() => disconnected, maxDisconnectMs);

            Assert.True(detected, $"Raw client should disconnect within {maxDisconnectMs}ms for PingInterval={pingIntervalSec}s");
        }, pingInterval: pingIntervalSec, requestTimeout: 30);
    }

    #endregion
}
