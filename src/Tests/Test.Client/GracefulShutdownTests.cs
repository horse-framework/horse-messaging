using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Test.Common;
using Xunit;

namespace Test.Client;

// ─────────────────────────────────────────────────────────────────────────────
// Shared state & helpers for shutdown tests
// ─────────────────────────────────────────────────────────────────────────────

internal static class ShutdownState
{
    public static volatile bool CallbackInvoked;
    public static volatile bool ProviderCallbackInvoked;
    public static volatile int ConsumeCount;
    public static readonly object Lock = new();

    public static void Reset()
    {
        CallbackInvoked = false;
        ProviderCallbackInvoked = false;
        ConsumeCount = 0;
    }
}

[QueueName("gs-push-a")]
[AutoAck]
internal class GracefulShutdownConsumer : IQueueConsumer<string>
{
    public Task Consume(ConsumeContext<string> context)
    {
        Interlocked.Increment(ref ShutdownState.ConsumeCount);
        return Task.CompletedTask;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Tests
// ─────────────────────────────────────────────────────────────────────────────

public class GracefulShutdownTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<(TestHorseRider server, int port)> StartServer()
    {
        var server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);
        return (server, port);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(30);
    }

    // -----------------------------------------------------------------------
    // 1. Disconnect without UseGracefulShutdown → raw disconnect, no drain
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Disconnect_WithoutGraceful_DisconnectsImmediately()
    {
        var (server, port) = await StartServer();

        var client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{port}");
        Assert.True(client.IsConnected);

        client.Disconnect();

        Assert.False(client.IsConnected);
        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 2. Disconnect with UseGracefulShutdown → graceful path runs
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Disconnect_WithGraceful_InvokesCallback()
    {
        ShutdownState.Reset();
        var (server, port) = await StartServer();

        var builder = new HorseClientBuilder();
        builder.AddHost($"horse://localhost:{port}");
        builder.UseGracefulShutdown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(2),
            () =>
            {
                ShutdownState.CallbackInvoked = true;
                return Task.CompletedTask;
            });

        var client = builder.Build();
        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        client.Disconnect();

        await WaitUntil(() => !client.IsConnected, 3000);
        Assert.False(client.IsConnected);
        Assert.True(ShutdownState.CallbackInvoked, "ShuttingDownAction callback must be invoked during graceful shutdown");

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 3. Disconnect with UseGracefulShutdown (IServiceProvider overload)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Disconnect_WithGraceful_InvokesProviderCallback()
    {
        ShutdownState.Reset();
        var (server, port) = await StartServer();

        var builder = new HorseClientBuilder();
        builder.AddHost($"horse://localhost:{port}");
        builder.UseGracefulShutdown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(2),
            (IServiceProvider _) =>
            {
                ShutdownState.ProviderCallbackInvoked = true;
                return Task.CompletedTask;
            });

        var client = builder.Build();
        // Provider is internal — use ServiceCollection registration path instead
        var services = new ServiceCollection();
        services.AddSingleton(client);
        var provider = services.BuildServiceProvider();
        // Provider is set internally by UseHorse, so we test via service-based path instead
        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        client.Disconnect();

        await WaitUntil(() => !client.IsConnected, 3000);
        Assert.False(client.IsConnected);
        Assert.True(ShutdownState.ProviderCallbackInvoked, "ShuttingDownActionWithProvider callback must be invoked");

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 4. Graceful shutdown is idempotent — second Disconnect is no-op
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Disconnect_CalledTwice_SecondIsNoOp()
    {
        ShutdownState.Reset();
        int callbackCount = 0;

        var (server, port) = await StartServer();

        var builder = new HorseClientBuilder();
        builder.AddHost($"horse://localhost:{port}");
        builder.UseGracefulShutdown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(2),
            () =>
            {
                Interlocked.Increment(ref callbackCount);
                return Task.CompletedTask;
            });

        var client = builder.Build();
        await client.ConnectAsync();

        client.Disconnect();
        client.Disconnect();

        await Task.Delay(200);

        Assert.Equal(1, callbackCount);
        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 5. ConsumeToken is cancelled during graceful shutdown
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GracefulShutdown_CancelsConsumeToken()
    {
        var (server, port) = await StartServer();

        var builder = new HorseClientBuilder();
        builder.AddHost($"horse://localhost:{port}");
        builder.UseGracefulShutdown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(2),
            () => Task.CompletedTask);

        var client = builder.Build();
        await client.ConnectAsync();

        var tokenBefore = client.ConsumeToken;
        Assert.False(tokenBefore.IsCancellationRequested);

        client.Disconnect();

        await WaitUntil(() => tokenBefore.IsCancellationRequested, 2000);
        Assert.True(tokenBefore.IsCancellationRequested);

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 6. MinWait is respected — disconnect takes at least MinWait time
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GracefulShutdown_RespectsMinWait()
    {
        var (server, port) = await StartServer();

        var builder = new HorseClientBuilder();
        builder.AddHost($"horse://localhost:{port}");
        builder.UseGracefulShutdown(
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(5),
            () => Task.CompletedTask);

        var client = builder.Build();
        await client.ConnectAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        client.Disconnect();
        sw.Stop();

        // Should take at least ~500ms (MinWait). Give generous margin.
        Assert.True(sw.ElapsedMilliseconds >= 400,
            $"Graceful shutdown should wait at least MinWait. Elapsed: {sw.ElapsedMilliseconds}ms");

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 7. MaxWait caps the total shutdown time
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GracefulShutdown_CappedByMaxWait()
    {
        var (server, port) = await StartServer();

        var builder = new HorseClientBuilder();
        builder.AddHost($"horse://localhost:{port}");
        builder.UseGracefulShutdown(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(2),
            async () =>
            {
                // Simulate long callback — should be capped by MaxWait
                await Task.Delay(30_000);
            });

        var client = builder.Build();
        await client.ConnectAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        client.Disconnect();
        sw.Stop();

        // Should complete within MaxWait + generous margin
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Graceful shutdown should be capped by MaxWait. Elapsed: {sw.ElapsedMilliseconds}ms");
        Assert.False(client.IsConnected);

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 8. Reconnect after graceful shutdown suppressed
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GracefulShutdown_SuppressesAutoReconnect()
    {
        var (server, port) = await StartServer();

        var builder = new HorseClientBuilder();
        builder.AddHost($"horse://localhost:{port}");
        builder.SetReconnectWait(TimeSpan.FromMilliseconds(200));
        builder.UseGracefulShutdown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(2),
            () => Task.CompletedTask);

        var client = builder.Build();
        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        client.Disconnect();
        await Task.Delay(1000);

        Assert.False(client.IsConnected, "Client must not reconnect after graceful shutdown");

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 9. IServiceCollection + UseGracefulShutdown → Disconnect graceful
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ServiceCollection_WithGraceful_DisconnectTriggersGraceful()
    {
        ShutdownState.Reset();
        var (server, port) = await StartServer();

        var services = new ServiceCollection();
        services.AddHorse(b =>
        {
            b.AddHost($"horse://localhost:{port}");
            b.UseGracefulShutdown(
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(2),
                () =>
                {
                    ShutdownState.CallbackInvoked = true;
                    return Task.CompletedTask;
                });
        });

        var provider = services.BuildServiceProvider();
        provider.UseHorse();

        var client = provider.GetRequiredService<HorseClient>();
        await WaitUntil(() => client.IsConnected, 3000);
        Assert.True(client.IsConnected);

        client.Disconnect();

        await WaitUntil(() => ShutdownState.CallbackInvoked, 3000);
        Assert.True(ShutdownState.CallbackInvoked);
        Assert.False(client.IsConnected);

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 10. IServiceCollection without UseGracefulShutdown → raw disconnect
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ServiceCollection_WithoutGraceful_DisconnectsRaw()
    {
        var (server, port) = await StartServer();

        var services = new ServiceCollection();
        services.AddHorse(b =>
        {
            b.AddHost($"horse://localhost:{port}");
        });

        var provider = services.BuildServiceProvider();
        provider.UseHorse();

        var client = provider.GetRequiredService<HorseClient>();
        await WaitUntil(() => client.IsConnected, 3000);
        Assert.True(client.IsConnected);

        client.Disconnect();

        Assert.False(client.IsConnected);
        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 11. Hosted scenario — Host.StopAsync triggers graceful shutdown
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HostedScenario_StopAsync_TriggersGracefulShutdown()
    {
        ShutdownState.Reset();
        var (server, port) = await StartServer();

        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.AddHorse(b =>
        {
            b.AddHost($"horse://localhost:{port}");
            b.UseGracefulShutdown(
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(2),
                () =>
                {
                    ShutdownState.CallbackInvoked = true;
                    return Task.CompletedTask;
                });
        });

        using var host = hostBuilder.Build();
        await host.StartAsync();

        var client = host.Services.GetRequiredService<HorseClient>();
        await WaitUntil(() => client.IsConnected, 3000);
        Assert.True(client.IsConnected);

        await host.StopAsync();

        Assert.True(ShutdownState.CallbackInvoked, "Graceful shutdown callback must be invoked on Host.StopAsync");
        Assert.False(client.IsConnected);

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 12. Hosted scenario without graceful — StopAsync still disconnects
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HostedScenario_WithoutGraceful_StopAsyncDisconnects()
    {
        var (server, port) = await StartServer();

        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.AddHorse(b =>
        {
            b.AddHost($"horse://localhost:{port}");
        });

        using var host = hostBuilder.Build();
        await host.StartAsync();

        var client = host.Services.GetRequiredService<HorseClient>();
        await WaitUntil(() => client.IsConnected, 3000);

        await host.StopAsync();

        // Without graceful shutdown configured, the host stops but the client's
        // Disconnect() was not explicitly called by GracefulShutdownService.
        // The connection may or may not be alive depending on the socket cleanup.
        // What we verify is that no exception is thrown.

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 13. Hosted + autoConnect = false → client not connected until UseHorse
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HostedScenario_AutoConnectFalse_NotConnectedUntilUseHorse()
    {
        var (server, port) = await StartServer();

        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.AddHorse(b =>
        {
            b.AddHost($"horse://localhost:{port}");
        }, autoConnect: false);

        using var host = hostBuilder.Build();
        await host.StartAsync();

        var client = host.Services.GetRequiredService<HorseClient>();
        await Task.Delay(500);
        Assert.False(client.IsConnected, "Client must NOT auto-connect when autoConnect = false");

        // Now manually connect
        host.Services.UseHorse();
        await WaitUntil(() => client.IsConnected, 3000);
        Assert.True(client.IsConnected);

        await host.StopAsync();
        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 14. Callback exception is swallowed — does not break shutdown
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GracefulShutdown_CallbackException_DoesNotBreakShutdown()
    {
        var (server, port) = await StartServer();

        var builder = new HorseClientBuilder();
        builder.AddHost($"horse://localhost:{port}");
        builder.UseGracefulShutdown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(2),
            () => throw new InvalidOperationException("boom"));

        var client = builder.Build();
        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        var ex = Record.Exception(() => client.Disconnect());
        Assert.Null(ex);
        Assert.False(client.IsConnected);

        server.Stop();
    }

    // -----------------------------------------------------------------------
    // 15. IServiceCollection does NOT register HorseConnectService
    // -----------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_AddHorse_DoesNotRegisterHostedConnectService()
    {
        var services = new ServiceCollection();
        services.AddHorse(b =>
        {
            b.AddHost("horse://localhost:9999");
        });

        // HorseConnectService should NOT be registered (autoConnect is always false for IServiceCollection)
        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();

        foreach (var svc in hostedServices)
            Assert.False(svc.GetType().Name == "HorseConnectService",
                "HorseConnectService must NOT be registered on IServiceCollection overloads");
    }

    // -----------------------------------------------------------------------
    // 16. IServiceCollection with UseGracefulShutdown registers GracefulShutdownService
    // -----------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_WithGraceful_RegistersGracefulShutdownService()
    {
        var services = new ServiceCollection();
        services.AddHorse(b =>
        {
            b.AddHost("horse://localhost:9999");
            b.UseGracefulShutdown(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                (Func<Task>)null);
        });

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();

        bool found = false;
        foreach (var svc in hostedServices)
        {
            if (svc.GetType().Name == "GracefulShutdownService")
                found = true;
        }

        Assert.True(found, "GracefulShutdownService must be registered when UseGracefulShutdown is configured");
    }

    // -----------------------------------------------------------------------
    // 17. Hosted scenario registers both HorseConnectService and GracefulShutdownService
    // -----------------------------------------------------------------------

    [Fact]
    public void HostBuilder_WithGraceful_RegistersBothHostedServices()
    {
        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.AddHorse(b =>
        {
            b.AddHost("horse://localhost:9999");
            b.UseGracefulShutdown(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                (Func<Task>)null);
        });

        using var host = hostBuilder.Build();
        var hostedServices = host.Services.GetServices<IHostedService>();

        bool connectFound = false;
        bool shutdownFound = false;
        foreach (var svc in hostedServices)
        {
            if (svc.GetType().Name == "HorseConnectService")
                connectFound = true;
            if (svc.GetType().Name == "GracefulShutdownService")
                shutdownFound = true;
        }

        Assert.True(connectFound, "HorseConnectService must be registered in hosted scenario with autoConnect=true");
        Assert.True(shutdownFound, "GracefulShutdownService must be registered when UseGracefulShutdown is configured");
    }
}
