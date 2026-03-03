using System;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Client-server integration tests: client sets/gets/removes cache via HorseClient.
/// </summary>
public class CacheClientIntegrationTest
{
    private static async Task<HorseClient> Connect(int port)
    {
        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{port}");
        return client;
    }

    [Fact]
    public async Task Client_SetString_GetString_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        HorseResult setResult = await client.Cache.SetString("c-key", "c-val", TimeSpan.FromMinutes(5));
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetString("c-key");
        Assert.NotNull(getResult);
        Assert.Equal("c-val", getResult.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetData_GetData_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        byte[] data = Encoding.UTF8.GetBytes("binary-payload");
        HorseResult setResult = await client.Cache.SetData("bin-key", data, TimeSpan.FromMinutes(5));
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetData("bin-key");
        Assert.NotNull(getResult);
        Assert.Equal(data, getResult.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Remove_KeyGone()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("rm-c", "val", TimeSpan.FromMinutes(5));
        HorseResult rmResult = await client.Cache.Remove("rm-c");
        Assert.Equal(HorseResultCode.Ok, rmResult.Code);

        var getResult = await client.Cache.GetString("rm-c");
        Assert.Null(getResult);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Purge_AllKeysGone()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("purge-1", "a", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("purge-2", "b", TimeSpan.FromMinutes(5));

        HorseResult purgeResult = await client.Cache.Purge();
        Assert.Equal(HorseResultCode.Ok, purgeResult.Code);

        Assert.Null(await client.Cache.GetString("purge-1"));
        Assert.Null(await client.Cache.GetString("purge-2"));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_PurgeByTag()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("tag-1", "a", TimeSpan.FromMinutes(5), tags: ["grp"]);
        await client.Cache.SetString("tag-2", "b", TimeSpan.FromMinutes(5));

        await client.Cache.PurgeByTag("grp");

        Assert.Null(await client.Cache.GetString("tag-1"));
        Assert.NotNull(await client.Cache.GetString("tag-2"));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetNonExistent_ReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        var result = await client.Cache.GetString("no-such-key");
        Assert.Null(result);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Set_WithTags_VerifiedByPurgeByTag()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("t-key", "data", TimeSpan.FromMinutes(5), tags: ["session"]);

        Assert.NotNull(await client.Cache.GetString("t-key"));

        await client.Cache.PurgeByTag("session");

        Assert.Null(await client.Cache.GetString("t-key"));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_TwoClients_SharedCache()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient writer = await Connect(ctx.Port);
        HorseClient reader = await Connect(ctx.Port);

        await writer.Cache.SetString("shared", "from-writer", TimeSpan.FromMinutes(5));

        var result = await reader.Cache.GetString("shared");
        Assert.NotNull(result);
        Assert.Equal("from-writer", result.Value);

        writer.Disconnect();
        reader.Disconnect();
    }

    [Fact]
    public async Task Client_List_ReturnsCacheKeys()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("list-1", "a", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("list-2", "b", TimeSpan.FromMinutes(5));

        var listResult = await client.Cache.List();
        Assert.Equal(HorseResultCode.Ok, listResult.Result.Code);
        Assert.True(listResult.Model.Count >= 2);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_ValueIncreases()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("inc-key", TimeSpan.FromMinutes(5));
        Assert.NotNull(r1);
        Assert.Equal(1, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("inc-key", TimeSpan.FromMinutes(5));
        Assert.NotNull(r2);
        Assert.Equal(2, r2.Value);

        var r3 = await client.Cache.GetIncrementalValue("inc-key", TimeSpan.FromMinutes(5));
        Assert.NotNull(r3);
        Assert.Equal(3, r3.Value);

        client.Disconnect();
    }
}

