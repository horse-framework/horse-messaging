using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Protocol;
using Test.Common;
using Test.Events.Handlers.Cache;
using Xunit;

namespace Test.Events;

public class CacheEventTest
{
    [Fact]
    public async Task CacheGet()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        HorseClient client = new HorseClient();

        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
        registrar.RegisterHandler<CacheGetHandler>();

        await client.ConnectAsync($"horse://localhost:{port}");

        HorseResult cacheResult = await client.Cache.SetString("cache-key", "Hello, World!");
        Assert.Equal(HorseResultCode.Ok, cacheResult.Code);

        var data = await client.Cache.GetString("cache-key");
        Assert.Equal("Hello, World!", data.Value);

        await Task.Delay(250);
        Assert.Equal(1, CacheGetHandler.Count);
        server.Stop();
    }

    [Fact]
    public async Task CacheSet()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        HorseClient client = new HorseClient();

        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
        registrar.RegisterHandler<CacheSetHandler>();

        await client.ConnectAsync($"horse://localhost:{port}");

        HorseResult cacheResult = await client.Cache.SetString("cache-key", "Hello, World!");
        Assert.Equal(HorseResultCode.Ok, cacheResult.Code);

        await Task.Delay(250);
        Assert.Equal(1, CacheSetHandler.Count);
        server.Stop();
    }

    [Fact]
    public async Task CacheRemove()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        HorseClient client = new HorseClient();

        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
        registrar.RegisterHandler<CacheRemoveHandler>();

        await client.ConnectAsync($"horse://localhost:{port}");

        HorseResult cacheResult = await client.Cache.SetString("cache-key", "Hello, World!");
        Assert.Equal(HorseResultCode.Ok, cacheResult.Code);

        HorseResult removeResult = await client.Cache.Remove("cache-key");
        Assert.Equal(HorseResultCode.Ok, removeResult.Code);

        await Task.Delay(250);
        Assert.Equal(1, CacheRemoveHandler.Count);
        server.Stop();
    }

    [Fact]
    public async Task CachePurge()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        HorseClient client = new HorseClient();

        EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
        registrar.RegisterHandler<CachePurgeHandler>();

        await client.ConnectAsync($"horse://localhost:{port}");

        HorseResult cacheResult = await client.Cache.SetString("cache-key", "Hello, World!");
        Assert.Equal(HorseResultCode.Ok, cacheResult.Code);

        HorseResult purgeResult = await client.Cache.Purge();
        Assert.Equal(HorseResultCode.Ok, purgeResult.Code);

        await Task.Delay(250);
        Assert.Equal(1, CachePurgeHandler.Count);
        server.Stop();
    }
}