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
/// Tests that reproduce and diagnose the production issue where an idle producer
/// (a client that connects but sends no messages) gets disconnected unexpectedly.
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

    [Fact]
    public async Task IdleProducer_DefaultSettings_StaysConnected60s()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.SetClientName("idle_producer_test");

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

            await Task.Delay(60_000);

            Assert.True(client.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"PRODUCTION BUG: Idle producer disconnected after {disconnectAfterSec.Value:F1}s"
                    : "Idle producer should remain connected for 60s");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(3, 15)]
    [InlineData(5, 30)]
    [InlineData(3, 60)]
    [InlineData(10, 10)]
    [InlineData(120, 15)]
    public async Task IdleProducer_PingIntervalCombinations_StaysAlive(int serverPingSec, int clientPingSec)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.PingInterval = TimeSpan.FromSeconds(clientPingSec);
            client.SetClientName($"idle-sv{serverPingSec}-cl{clientPingSec}");

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            int waitMs = Math.Min(Math.Max(serverPingSec, clientPingSec) * 3 * 1000, 30_000);
            waitMs = Math.Max(waitMs, 10_000);

            await Task.Delay(waitMs);

            Assert.True(client.IsConnected,
                $"Idle producer (server ping={serverPingSec}s, client ping={clientPingSec}s) must survive {waitMs / 1000}s idle");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: serverPingSec, requestTimeout: 120);
    }

    #endregion

    #region SmartHealthCheck Mismatch

    [Fact]
    public async Task SmartHealthCheckMismatch_DefaultFalse_StaysConnected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            Assert.True(client.SmartHealthCheck, "HorseClient.SmartHealthCheck should default to true");
            client.SmartHealthCheck = false;
            Assert.Equal(TimeSpan.FromSeconds(15), client.PingInterval);

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            await Task.Delay(20_000);

            Assert.True(client.IsConnected,
                "SmartHealthCheck=false client should stay connected via auto PONG responses to server PINGs");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 2, requestTimeout: 15);
    }

    [Fact]
    public async Task SmartHealthCheck_Enabled_IdleProducer_StaysConnected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.SmartHealthCheck = true;
            client.PingInterval = TimeSpan.FromSeconds(15);

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            await Task.Delay(20_000);

            Assert.True(client.IsConnected, "SmartHealthCheck=true idle producer should survive via server pings");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 2, requestTimeout: 15);
    }

    #endregion

    #region Intermittent Traffic Patterns

    [Fact]
    public async Task IntermittentProducer_SendsEvery10s_StaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.SetClientName("intermittent_producer");

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            for (int i = 0; i < 6; i++)
            {
                await Task.Delay(10_000);

                if (!client.IsConnected)
                    break;

                HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "test-idle-queue");
                msg.SetStringContent($"msg-{i}");
                await client.SendAsync(msg, CancellationToken.None);
            }

            Assert.True(client.IsConnected, "Intermittent producer should stay connected across idle-send-idle cycles");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task BurstThenIdle30s_StaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.SetClientName("burst_then_idle_producer");

            bool disconnected = false;
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            for (int i = 0; i < 20; i++)
            {
                HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "test-burst-queue");
                msg.SetStringContent($"burst-{i}");
                await client.SendAsync(msg, CancellationToken.None);
                await Task.Delay(50);
            }

            await Task.Delay(30_000);

            Assert.True(client.IsConnected, "Producer must survive 30s idle after burst — server pings keep it alive");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion

    #region Reconnection Stability

    [Fact]
    public async Task Reconnect_AfterServerRestart_SecondSessionStable()
    {
        await TestHorseRider.RunWith(async (server1, port1) =>
        {
            Assert.True(port1 > 0);

            HorseClient client1 = new HorseClient();
            client1.SetClientName("reconnect_test_producer");

            bool disconnected1 = false;
            client1.Disconnected += _ => disconnected1 = true;
            await client1.ConnectAsync($"horse://localhost:{port1}");
            await Task.Delay(500);
            Assert.True(client1.IsConnected);

            await server1.StopAsync();
            await WaitUntil(() => disconnected1, 15_000);
            client1.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);

        await TestHorseRider.RunWith(async (server2, port2) =>
        {
            Assert.True(port2 > 0);

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

            await Task.Delay(45_000);

            Assert.True(client2.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"Second session disconnected after {disconnectAfterSec.Value:F1}s — matches production bug pattern"
                    : "Reconnected producer must remain stable");
            Assert.False(disconnected2);

            client2.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion

    #region Multiple Idle Producers

    [Fact]
    public async Task TenIdleProducers_AllSurvive30s()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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

            await Task.Delay(30_000);

            for (int i = 0; i < clients.Count; i++)
            {
                Assert.True(clients[i].IsConnected, $"Idle producer {i} must survive 30s");
                Assert.False(disconnectedFlags[i], $"No disconnect for producer {i}");
            }

            foreach (HorseClient c in clients)
                c.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion

    #region Aggressive Server PingInterval

    [Fact]
    public async Task AggressiveServerPing1s_IdleProducer_Survives30s()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.SetClientName("aggressive_ping_producer");

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

            await Task.Delay(30_000);

            Assert.True(client.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"Idle producer disconnected after {disconnectAfterSec.Value:F1}s with PingInterval=1s — potential production bug!"
                    : "Idle producer must survive aggressive server pinging (1s)");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 1, requestTimeout: 10);
    }

    #endregion

    #region RequestTimeout Interaction

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    public async Task RequestTimeout_DoesNotKillEstablishedConnection(int requestTimeoutSec)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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

            int waitMs = (requestTimeoutSec + 10) * 1000;
            await Task.Delay(waitMs);

            Assert.True(client.IsConnected,
                disconnectAfterSec.HasValue
                    ? $"Connection killed after {disconnectAfterSec.Value:F1}s — RequestTimeout={requestTimeoutSec}s may be killing established connections!"
                    : $"Established connection must survive beyond RequestTimeout={requestTimeoutSec}s");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: requestTimeoutSec);
    }

    #endregion

    #region Diagnostic: Timing Analysis

    [Fact]
    public async Task Diagnostic_TrackDisconnectTiming()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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

            await Task.Delay(45_000);

            string timeline;
            lock (events)
                timeline = string.Join(" → ", events.Select(e => $"{e.Event}@{e.Seconds:F1}s"));

            Assert.True(client.IsConnected,
                $"Idle producer disconnected! Timeline: [{timeline}]. Production pattern: Connected@0s → Disconnected@9s");

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 10);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(45)]
    [InlineData(60)]
    public async Task ProductionExactMatch_CrowPlaygroundProducer(int idleSeconds)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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
                    ? $"crow_play_ground_producer disconnected after {disconnectAfterSec.Value:F1}s (expected >{idleSeconds}s). PRODUCTION BUG REPRODUCED!"
                    : $"crow_play_ground_producer survived {idleSeconds}s idle ✓");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 5, requestTimeout: 30);
    }

    #endregion

    #region HorseClientBuilder: Default Configuration Stability

    [Fact]
    public async Task HorseClientBuilder_DefaultConfig_IdleProducerStaysAlive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

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

            Assert.True(client.IsConnected, "Builder-created idle producer should stay connected 30s");
            Assert.False(disconnected);

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion
}
