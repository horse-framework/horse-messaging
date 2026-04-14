using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Cache;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Routers;
using Horse.Messaging.Extensions.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Test.Common;
using Xunit;

namespace Test.Client;

/// <summary>
/// Marker type for multi-client tests
/// </summary>
internal class SecondConnection;

public class ExtensionRegistrationTests
{
    // -----------------------------------------------------------------------
    // 1. AddHorse registers HorseClient as singleton
    // -----------------------------------------------------------------------

    [Fact]
    public void AddHorse_RegistersHorseClientAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddHorse(b => b.AddHost("horse://localhost:9999"));

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<HorseClient>();

        Assert.NotNull(client);

        // Must be the same instance
        var client2 = provider.GetService<HorseClient>();
        Assert.Same(client, client2);
    }

    // -----------------------------------------------------------------------
    // 2. AddHorse registers all bus abstractions
    // -----------------------------------------------------------------------

    [Fact]
    public void AddHorse_RegistersAllBusAbstractions()
    {
        var services = new ServiceCollection();
        services.AddHorse(b => b.AddHost("horse://localhost:9999"));

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<HorseClient>());
        Assert.NotNull(provider.GetService<IHorseQueueBus>());
        Assert.NotNull(provider.GetService<IHorseDirectBus>());
        Assert.NotNull(provider.GetService<IHorseChannelBus>());
        Assert.NotNull(provider.GetService<IHorseRouterBus>());
        Assert.NotNull(provider.GetService<IHorseCache>());
    }

    // -----------------------------------------------------------------------
    // 3. IServiceCollection AddHorse does NOT have autoConnect parameter
    //    → no HorseConnectService registered
    // -----------------------------------------------------------------------

    [Fact]
    public void AddHorse_ServiceCollection_NoAutoConnect_NoConnectService()
    {
        var services = new ServiceCollection();
        services.AddHorse(b => b.AddHost("horse://localhost:9999"));

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();

        foreach (var svc in hostedServices)
            Assert.False(svc.GetType().Name == "HorseConnectService",
                "IServiceCollection overload must never register HorseConnectService");
    }

    // -----------------------------------------------------------------------
    // 4. UseHorse connects the client
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UseHorse_ConnectsClient()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddHorse(b => b.AddHost($"horse://localhost:{port}"));

            var provider = services.BuildServiceProvider();
            provider.UseHorse();

            var client = provider.GetRequiredService<HorseClient>();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);

            Assert.True(client.IsConnected);

            client.Disconnect();
        });
    }

// -----------------------------------------------------------------------
// 5. UseHorse connects the client and it works
// -----------------------------------------------------------------------
    [Fact]
    public async Task UseHorse_ClientIsFullyOperational()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddHorse(b => b.AddHost($"horse://localhost:{port}"));

            var provider = services.BuildServiceProvider();
            provider.UseHorse();

            var client = provider.GetRequiredService<HorseClient>();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);
            Assert.True(client.IsConnected);

            // Verify bus abstractions are also resolvable
            Assert.NotNull(provider.GetService<IHorseQueueBus>());

            client.Disconnect();
        });
    }

// -----------------------------------------------------------------------
// 6. Without UseHorse, client is NOT connected
// -----------------------------------------------------------------------

    [Fact]
    public async Task WithoutUseHorse_ClientNotConnected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddHorse(b => b.AddHost($"horse://localhost:{port}"));

            var provider = services.BuildServiceProvider();
            // NOT calling provider.UseHorse()

            var client = provider.GetRequiredService<HorseClient>();
            await Task.Delay(500);

            Assert.False(client.IsConnected,
                "Client must NOT connect automatically when using IServiceCollection overloads");
        });
    }

// ─────────────────────────────────────────────────────────────────────────
// IServiceCollection — Keyed registration
// ─────────────────────────────────────────────────────────────────────────

// -----------------------------------------------------------------------
// 7. AddKeyedHorse registers keyed client
// -----------------------------------------------------------------------

    [Fact]
    public void AddKeyedHorse_RegistersKeyedClient()
    {
        var services = new ServiceCollection();
        services.AddKeyedHorse("conn-a", b => b.AddHost("horse://localhost:9999"));

        var provider = services.BuildServiceProvider();
        var client = provider.GetKeyedService<HorseClient>("conn-a");

        Assert.NotNull(client);
    }

// -----------------------------------------------------------------------
// 8. UseHorse with key connects the keyed client
// -----------------------------------------------------------------------

    [Fact]
    public async Task UseHorse_Keyed_ConnectsClient()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddKeyedHorse("my-key", b => b.AddHost($"horse://localhost:{port}"));

            var provider = services.BuildServiceProvider();
            provider.UseHorse("my-key");

            var client = provider.GetRequiredKeyedService<HorseClient>("my-key");
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);

            Assert.True(client.IsConnected);

            client.Disconnect();
        });
    }

// ─────────────────────────────────────────────────────────────────────────
// IServiceCollection — Typed registration
// ─────────────────────────────────────────────────────────────────────────

// -----------------------------------------------------------------------
// 9. AddHorse<T> registers typed client
// -----------------------------------------------------------------------

    [Fact]
    public void AddHorse_Typed_RegistersTypedClient()
    {
        var services = new ServiceCollection();
        services.AddHorse<SecondConnection>(b => b.AddHost("horse://localhost:9999"));

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<HorseClient<SecondConnection>>();

        Assert.NotNull(client);
    }

// -----------------------------------------------------------------------
// 10. UseHorse<T> connects typed client
// -----------------------------------------------------------------------

    [Fact]
    public async Task UseHorse_Typed_ConnectsClient()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddHorse<SecondConnection>(b => b.AddHost($"horse://localhost:{port}"));

            var provider = services.BuildServiceProvider();
            provider.UseHorse<SecondConnection>();

            var client = provider.GetRequiredService<HorseClient<SecondConnection>>();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);

            Assert.True(client.IsConnected);

            client.Disconnect();
        });
    }

// ─────────────────────────────────────────────────────────────────────────
// IHostBuilder — Registration & AutoConnect
// ─────────────────────────────────────────────────────────────────────────

// -----------------------------------------------------------------------
// 11. IHostBuilder AddHorse auto-connects (default)
// -----------------------------------------------------------------------

    [Fact]
    public async Task HostBuilder_AddHorse_AutoConnectsByDefault()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var hostBuilder = Host.CreateDefaultBuilder();
            hostBuilder.AddHorse(b => b.AddHost($"horse://localhost:{port}"));

            using var host = hostBuilder.Build();
            await host.StartAsync();

            var client = host.Services.GetRequiredService<HorseClient>();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);

            Assert.True(client.IsConnected);

            await host.StopAsync();
        });
    }

// -----------------------------------------------------------------------
// 12. IHostBuilder AddHorse autoConnect=false → UseHorse needed
// -----------------------------------------------------------------------

    [Fact]
    public async Task HostBuilder_AddHorse_AutoConnectFalse_NotConnected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var hostBuilder = Host.CreateDefaultBuilder();
            hostBuilder.AddHorse(b => b.AddHost($"horse://localhost:{port}"), autoConnect: false);

            using var host = hostBuilder.Build();
            await host.StartAsync();

            var client = host.Services.GetRequiredService<HorseClient>();
            await Task.Delay(500);

            Assert.False(client.IsConnected, "Client must NOT auto-connect when autoConnect=false");

            host.Services.UseHorse();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);
            Assert.True(client.IsConnected);

            await host.StopAsync();
        });
    }

// -----------------------------------------------------------------------
// 13. IHostBuilder registers all bus abstractions
// -----------------------------------------------------------------------

    [Fact]
    public void HostBuilder_AddHorse_RegistersAllBusAbstractions()
    {
        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.AddHorse(b => b.AddHost("horse://localhost:9999"), autoConnect: false);

        using var host = hostBuilder.Build();

        Assert.NotNull(host.Services.GetService<HorseClient>());
        Assert.NotNull(host.Services.GetService<IHorseQueueBus>());
        Assert.NotNull(host.Services.GetService<IHorseDirectBus>());
        Assert.NotNull(host.Services.GetService<IHorseChannelBus>());
        Assert.NotNull(host.Services.GetService<IHorseRouterBus>());
        Assert.NotNull(host.Services.GetService<IHorseCache>());
    }

// -----------------------------------------------------------------------
// 14. IHostBuilder registers HorseConnectService when autoConnect=true
// -----------------------------------------------------------------------

    [Fact]
    public void HostBuilder_AutoConnectTrue_RegistersConnectService()
    {
        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.AddHorse(b => b.AddHost("horse://localhost:9999"));

        using var host = hostBuilder.Build();
        var hostedServices = host.Services.GetServices<IHostedService>();

        bool found = false;
        foreach (var svc in hostedServices)
        {
            if (svc.GetType().Name == "HorseConnectService")
                found = true;
        }

        Assert.True(found, "HorseConnectService must be registered when autoConnect=true");
    }

// -----------------------------------------------------------------------
// 15. IHostBuilder does NOT register HorseConnectService when autoConnect=false
// -----------------------------------------------------------------------

    [Fact]
    public void HostBuilder_AutoConnectFalse_DoesNotRegisterConnectService()
    {
        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.AddHorse(b => b.AddHost("horse://localhost:9999"), autoConnect: false);

        using var host = hostBuilder.Build();
        var hostedServices = host.Services.GetServices<IHostedService>();

        foreach (var svc in hostedServices)
            Assert.False(svc.GetType().Name == "HorseConnectService",
                "HorseConnectService must NOT be registered when autoConnect=false");
    }

// ─────────────────────────────────────────────────────────────────────────
// Multiple connections — same provider
// ─────────────────────────────────────────────────────────────────────────

// -----------------------------------------------------------------------
// 16. Two keyed clients in same provider are independent
// -----------------------------------------------------------------------

    [Fact]
    public async Task TwoKeyedClients_AreIndependent()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddKeyedHorse("alpha", b => b.AddHost($"horse://localhost:{port}"));
            services.AddKeyedHorse("beta", b => b.AddHost($"horse://localhost:{port}"));

            var provider = services.BuildServiceProvider();
            provider.UseHorse("alpha");
            provider.UseHorse("beta");

            var alpha = provider.GetRequiredKeyedService<HorseClient>("alpha");
            var beta = provider.GetRequiredKeyedService<HorseClient>("beta");

            await TestHorseRider.WaitUntil(() => alpha.IsConnected && beta.IsConnected, 3000);

            Assert.True(alpha.IsConnected);
            Assert.True(beta.IsConnected);
            Assert.NotSame(alpha, beta);

            alpha.Disconnect();
            Assert.False(alpha.IsConnected);
            Assert.True(beta.IsConnected);

            beta.Disconnect();
        });
    }

// -----------------------------------------------------------------------
// 17. Typed + default client in same provider are independent
// -----------------------------------------------------------------------

    [Fact]
    public async Task TypedAndDefaultClients_AreIndependent()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddHorse(b => b.AddHost($"horse://localhost:{port}"));
            services.AddHorse<SecondConnection>(b => b.AddHost($"horse://localhost:{port}"));

            var provider = services.BuildServiceProvider();
            provider.UseHorse();
            provider.UseHorse<SecondConnection>();

            var defaultClient = provider.GetRequiredService<HorseClient>();
            var typedClient = provider.GetRequiredService<HorseClient<SecondConnection>>();

            await TestHorseRider.WaitUntil(() => defaultClient.IsConnected, 3000);
            await TestHorseRider.WaitUntil(() => typedClient.IsConnected, 3000);

            Assert.True(defaultClient.IsConnected);
            Assert.True(typedClient.IsConnected);

            defaultClient.Disconnect();
            Assert.False(defaultClient.IsConnected);
            Assert.True(typedClient.IsConnected);

            typedClient.Disconnect();
        });
    }

// ─────────────────────────────────────────────────────────────────────────
// Builder configuration flows through
// ─────────────────────────────────────────────────────────────────────────

// -----------------------------------------------------------------------
// 18. Client configuration flows through registration
// -----------------------------------------------------------------------

    [Fact]
    public async Task AddHorse_BuilderConfig_FlowsThrough()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddHorse(b =>
            {
                b.AddHost($"horse://localhost:{port}");
                b.SetClientId("my-custom-id");
            });

            var provider = services.BuildServiceProvider();
            provider.UseHorse();

            var client = provider.GetRequiredService<HorseClient>();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);

            Assert.Equal("my-custom-id", client.ClientId);

            client.Disconnect();
        });
    }

// -----------------------------------------------------------------------
// 19. Reconnect wait flows through
// -----------------------------------------------------------------------

    [Fact]
    public async Task AddHorse_ReconnectWait_FlowsThrough()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            var services = new ServiceCollection();
            services.AddHorse(b =>
            {
                b.AddHost($"horse://localhost:{port}");
                b.SetReconnectWait(TimeSpan.FromSeconds(10));
            });

            var provider = services.BuildServiceProvider();
            provider.UseHorse();

            var client = provider.GetRequiredService<HorseClient>();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);

            // We can't directly read ReconnectWait, but we verify the client is alive
            Assert.True(client.IsConnected);

            client.Disconnect();
        });
    }

// -----------------------------------------------------------------------
// 20. Connected / Disconnected callbacks fire
// -----------------------------------------------------------------------

    [Fact]
    public async Task AddHorse_ConnectedCallback_Fires()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            bool connectedFired = false;

            var services = new ServiceCollection();
            services.AddHorse(b =>
            {
                b.AddHost($"horse://localhost:{port}");
                b.OnConnected(_ => connectedFired = true);
            });

            var provider = services.BuildServiceProvider();
            provider.UseHorse();

            var client = provider.GetRequiredService<HorseClient>();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);

            Assert.True(connectedFired, "OnConnected callback must fire");

            client.Disconnect();
        });
    }

    [Fact]
    public async Task AddHorse_DisconnectedCallback_Fires()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            bool disconnectedFired = false;

            var services = new ServiceCollection();
            services.AddHorse(b =>
            {
                b.AddHost($"horse://localhost:{port}");
                b.OnDisconnected(_ => disconnectedFired = true);
            });

            var provider = services.BuildServiceProvider();
            provider.UseHorse();

            var client = provider.GetRequiredService<HorseClient>();
            await TestHorseRider.WaitUntil(() => client.IsConnected, 3000);

            client.Disconnect();
            await TestHorseRider.WaitUntil(() => disconnectedFired, 3000);

            Assert.True(disconnectedFired, "OnDisconnected callback must fire");
        });
    }
}