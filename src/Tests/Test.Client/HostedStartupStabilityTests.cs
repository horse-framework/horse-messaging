using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Extensions.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Test.Common;
using Xunit;

namespace Test.Client;

internal sealed class StartupProbeState
{
    public volatile bool StartCalled;
    public volatile bool StopCalled;
    public volatile bool SawClientInstance;
}

internal sealed class StartupProbeHostedService(StartupProbeState state, HorseClient client) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        state.StartCalled = true;
        state.SawClientInstance = client != null;

        // Keep this hosted service alive briefly during startup to mimic a real host
        // that starts multiple background services together with the Horse client.
        await Task.Delay(250, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        state.StopCalled = true;
        return Task.CompletedTask;
    }
}

internal sealed class HostedClientLifecycleTimeline
{
    private readonly object _lock = new();
    private readonly List<string> _events = [];
    private readonly DateTime _start = DateTime.UtcNow;

    public int ConnectedCount { get; private set; }
    public int DisconnectedCount { get; private set; }

    public void Attach(HorseClient client)
    {
        client.Connected += OnConnected;
        client.Disconnected += OnDisconnected;
    }

    public void Detach(HorseClient client)
    {
        client.Connected -= OnConnected;
        client.Disconnected -= OnDisconnected;
    }

    public string Dump()
    {
        lock (_lock)
            return _events.Count == 0 ? "(no events)" : string.Join(" -> ", _events);
    }

    private void OnConnected(HorseClient client)
    {
        lock (_lock)
        {
            ConnectedCount++;
            _events.Add($"Connected@{(DateTime.UtcNow - _start).TotalSeconds:F1}s");
        }
    }

    private void OnDisconnected(HorseClient client)
    {
        lock (_lock)
        {
            DisconnectedCount++;
            _events.Add($"Disconnected@{(DateTime.UtcNow - _start).TotalSeconds:F1}s");
        }
    }
}

public class HostedStartupStabilityTests
{
    private static async Task RunWithServer(Func<TestHorseRider, int, Task> action)
    {
        await using var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);
        await action(server, port);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000, int pollMs = 50)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(pollMs);
    }

    [Fact]
    public async Task HostApplicationBuilder_AddHorse_DefaultIdleClient_DoesNotReconnectDuringStartupWindow()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.AddHorse(config =>
            {
                config.AddHost($"horse://localhost:{port}");
                config.SetClientName("crow_play_ground_producer");
                config.SetClientType("producer");
            });

            using IHost host = builder.Build();
            HorseClient client = host.Services.GetRequiredService<HorseClient>();
            HostedClientLifecycleTimeline timeline = new HostedClientLifecycleTimeline();
            timeline.Attach(client);

            try
            {
                await host.StartAsync();
                await WaitUntil(() => client.IsConnected && timeline.ConnectedCount > 0);

                Assert.True(client.IsConnected, $"Client should connect during host startup. Timeline: {timeline.Dump()}");
                Assert.Equal(1, timeline.ConnectedCount);
                Assert.Equal(0, timeline.DisconnectedCount);

                // Production symptom: first disconnect was around 9 seconds after startup.
                await Task.Delay(15_000);

                Assert.True(client.IsConnected, $"Idle hosted client disconnected unexpectedly. Timeline: {timeline.Dump()}");
                Assert.Equal(1, timeline.ConnectedCount);
                Assert.Equal(0, timeline.DisconnectedCount);
            }
            finally
            {
                timeline.Detach(client);
                await host.StopAsync();
            }
        });
    }

    [Fact]
    public async Task HostApplicationBuilder_WithAdditionalHostedService_DoesNotCycleConnectionOnStartup()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.AddHorse(config =>
            {
                config.AddHost($"horse://localhost:{port}");
                config.SetClientName("startup_probe_producer");
                config.SetClientType("producer");
            });

            builder.Services.AddSingleton<StartupProbeState>();
            builder.Services.AddHostedService<StartupProbeHostedService>();

            using IHost host = builder.Build();
            HorseClient client = host.Services.GetRequiredService<HorseClient>();
            StartupProbeState probeState = host.Services.GetRequiredService<StartupProbeState>();
            HostedClientLifecycleTimeline timeline = new HostedClientLifecycleTimeline();
            timeline.Attach(client);

            try
            {
                await host.StartAsync();
                await WaitUntil(() => client.IsConnected && timeline.ConnectedCount > 0 && probeState.StartCalled);

                Assert.True(probeState.StartCalled, "Additional hosted service should start together with HorseConnectService");
                Assert.True(probeState.SawClientInstance, "Additional hosted service should resolve the same HorseClient singleton");
                Assert.True(client.IsConnected, $"Client should remain connected after host startup. Timeline: {timeline.Dump()}");

                await Task.Delay(15_000);

                Assert.True(client.IsConnected, $"Client cycled during startup idle window. Timeline: {timeline.Dump()}");
                Assert.Equal(1, timeline.ConnectedCount);
                Assert.Equal(0, timeline.DisconnectedCount);
            }
            finally
            {
                timeline.Detach(client);
                await host.StopAsync();
                Assert.True(probeState.StopCalled, "Additional hosted service should stop cleanly with the host");
            }
        });
    }

    [Fact]
    public async Task HostApplicationBuilder_AggressiveServerHeartbeat_DoesNotDisconnectAndReconnect()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.AddHorse(config =>
            {
                config.AddHost($"horse://localhost:{port}");
                config.SetClientName("aggressive_heartbeat_producer");
                config.SetClientType("producer");
            });

            using IHost host = builder.Build();
            HorseClient client = host.Services.GetRequiredService<HorseClient>();
            HostedClientLifecycleTimeline timeline = new HostedClientLifecycleTimeline();
            timeline.Attach(client);

            try
            {
                await host.StartAsync();
                await WaitUntil(() => client.IsConnected && timeline.ConnectedCount > 0);

                Assert.True(client.IsConnected, $"Client should connect even with aggressive heartbeat. Timeline: {timeline.Dump()}");

                // Server PingInterval=1s is the closest stress profile to an early disconnect race.
                await Task.Delay(12_000);

                Assert.True(client.IsConnected, $"Aggressive heartbeat caused startup disconnect/reconnect cycle. Timeline: {timeline.Dump()}");
                Assert.Equal(1, timeline.ConnectedCount);
                Assert.Equal(0, timeline.DisconnectedCount);
            }
            finally
            {
                timeline.Detach(client);
                await host.StopAsync();
            }
        });
    }
}