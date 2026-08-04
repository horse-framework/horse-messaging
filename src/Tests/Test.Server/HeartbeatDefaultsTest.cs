using System;
using System.Threading.Tasks;
using Horse.Server;
using Xunit;

namespace Test.Server;

/// <summary>
/// Dead-peer detection must survive the dependency-injection configuration path.
///
/// <para>
/// <see cref="HeartbeatManager"/> is the ONLY mechanism that disconnects a client whose
/// socket died without a clean close (half-open connection): it pings idle sockets and
/// disconnects the ones that miss the pong grace period
/// (Horse.Server/HeartbeatManager.cs — PingClients).
/// </para>
///
/// <para>
/// It is created only when <c>PingInterval &gt; 0</c> (Horse.Server/HorseServer.cs:221).
/// <see cref="HorseServerOptions.CreateDefault"/> and the JSON-file loader both default it
/// to 120, but a plain <c>new HorseServerOptions()</c> leaves it at 0 — and that is exactly
/// what ASP.NET's <c>services.Configure&lt;HorseServerOptions&gt;(cfg.Bind)</c> produces when
/// the bound configuration section does not mention PingInterval.
/// </para>
///
/// <para>
/// Consequence observed in production (2026-08-04): heartbeat was never started, a consumer
/// whose pod was deleted stayed registered forever, it kept occupying the single subscriber
/// slot of a partitioned queue, no replacement consumer could ever be assigned, and the flow
/// died silently for ~19 hours with no exception and no log.
/// </para>
/// </summary>
public class HeartbeatDefaultsTest
{
    /// <summary>
    /// A default-constructed options object is what the DI binding path yields.
    /// It must not silently disable dead-peer detection.
    /// </summary>
    [Fact]
    public void DefaultConstructedOptions_EnableDeadPeerDetection()
    {
        HorseServerOptions options = new HorseServerOptions();

        Assert.True(options.PingInterval > 0,
            "new HorseServerOptions() leaves PingInterval = 0, which disables HeartbeatManager " +
            "and therefore all dead-peer detection. Half-open connections are then never reaped.");
    }

    /// <summary>
    /// The behavioural counterpart: a server started from default-constructed options
    /// must still run a heartbeat.
    /// </summary>
    [Fact]
    public async Task Server_StartedWithDefaultConstructedOptions_RunsHeartbeat()
    {
        HorseServerOptions options = new HorseServerOptions
        {
            Hosts = [new HorseHostOptions { Port = Random.Shared.Next(20000, 60000) }]
        };

        HorseServer server = new HorseServer(options);

        try
        {
            _ = server.StartAsync();
            await Task.Delay(300);

            Assert.NotNull(server.HeartbeatManager);
        }
        finally
        {
            try
            {
                await server.StopAsync();
            }
            catch
            {
                /* best effort */
            }
        }
    }

    /// <summary>
    /// Control case: the documented factory does enable heartbeat, which is why the
    /// defect only shows up through the DI/binding path.
    /// </summary>
    [Fact]
    public void CreateDefault_EnablesDeadPeerDetection()
    {
        Assert.True(HorseServerOptions.CreateDefault().PingInterval > 0);
    }
}
