using System;
using System.IO;
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

    [Fact]
    public async Task Client_GetExpiredKey_ReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        HorseClient client = await Connect(ctx.Port);

        // Client sends duration as int seconds, so minimum effective TTL is 1 second
        await client.Cache.SetString("exp-key", "val", TimeSpan.FromSeconds(1));
        await Task.Delay(1500);

        var result = await client.Cache.GetString("exp-key");
        Assert.Null(result);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetWithDuration_GetBeforeExpiry()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("dur-key", "dur-val", TimeSpan.FromSeconds(30));

        var result = await client.Cache.GetString("dur-key");
        Assert.NotNull(result);
        Assert.Equal("dur-val", result.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetPersistent_FileCreated()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("client-persist", "data", TimeSpan.FromMinutes(5), persistent: true);

        // Verify on server side that file exists
        string cacheDir = Path.Combine(ctx.Rider.Options.DataPath, "Cache");
        string filePath = Path.Combine(cacheDir, "client-persist.hci");
        Assert.True(File.Exists(filePath));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_CustomStep()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("step-key", TimeSpan.FromMinutes(5), 5);
        Assert.NotNull(r1);
        Assert.Equal(5, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("step-key", TimeSpan.FromMinutes(5), 5);
        Assert.NotNull(r2);
        Assert.Equal(10, r2.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_List_WithFilter_ReturnsMatchingKeys()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        // Purge to ensure clean state
        await client.Cache.Purge();

        await client.Cache.SetString("user:1", "a", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("user:2", "b", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("order:1", "c", TimeSpan.FromMinutes(5));

        var listResult = await client.Cache.List("user*");
        Assert.Equal(HorseResultCode.Ok, listResult.Result.Code);
        Assert.Equal(2, listResult.Model.Count);
        Assert.All(listResult.Model, info => Assert.StartsWith("user:", info.Key));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Overwrite_SameKey()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("ow-key", "first", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("ow-key", "second", TimeSpan.FromMinutes(5));

        var result = await client.Cache.GetString("ow-key");
        Assert.NotNull(result);
        Assert.Equal("second", result.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Remove_NonExistent_ReturnsOk()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        HorseResult result = await client.Cache.Remove("nonexistent-client-key");
        Assert.Equal(HorseResultCode.Ok, result.Code);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetWithTags_VerifiedByList()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);
        await client.Cache.Purge();

        await client.Cache.SetString("tag-list-key", "data", TimeSpan.FromMinutes(5), tags: ["sess", "user"]);

        var listResult = await client.Cache.List();
        Assert.Equal(HorseResultCode.Ok, listResult.Result.Code);
        Assert.Single(listResult.Model);
        Assert.Equal(2, listResult.Model[0].Tags.Length);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetString_ResponseMetadata_Populated()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("meta-key", "meta-val", TimeSpan.FromMinutes(10), tags: ["m-tag"]);

        var result = await client.Cache.GetString("meta-key");
        Assert.NotNull(result);
        Assert.Equal("meta-key", result.Key);
        Assert.Equal("meta-val", result.Value);
        Assert.True(result.Expiration > 0, "Expiration should be a positive unix timestamp");
        Assert.NotNull(result.Tags);
        Assert.Contains("m-tag", result.Tags);
        Assert.False(result.IsFirstWarnedClient);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetString_NoDuration_UsesDefaultDuration()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);

        HorseResult setResult = await client.Cache.SetString("no-dur", "val");
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetString("no-dur");
        Assert.NotNull(getResult);
        Assert.Equal("val", getResult.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetData_NoDuration_UsesDefaultDuration()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);

        byte[] data = [1, 2, 3, 4, 5];
        HorseResult setResult = await client.Cache.SetData("no-dur-data", data);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetData("no-dur-data");
        Assert.NotNull(getResult);
        Assert.Equal(data, getResult.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_NoDuration_UsesDefaultDuration()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("no-dur-inc");
        Assert.NotNull(r1);
        Assert.Equal(1, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("no-dur-inc");
        Assert.NotNull(r2);
        Assert.Equal(2, r2.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetString_WithWarningDuration()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        HorseClient client = await Connect(ctx.Port);

        HorseResult setResult = await client.Cache.SetString(
            "warn-dur-key", "val",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5));

        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var result2 = await client.Cache.GetString("warn-dur-key");
        Assert.NotNull(result2);
        Assert.Equal("val", result2.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_ValueMaxSize_Rejection()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ValueMaxSize = 50;
        });

        HorseClient client = await Connect(ctx.Port);

        byte[] bigData = new byte[200];
        HorseResult result3 = await client.Cache.SetData("too-big", bigData, TimeSpan.FromMinutes(5));
        Assert.Equal(HorseResultCode.ValueSizeLimit, result3.Code);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_MaximumKeys_Rejection()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 2;
        });

        HorseClient client = await Connect(ctx.Port);
        await client.Cache.Purge();

        await client.Cache.SetString("lim-1", "a", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("lim-2", "b", TimeSpan.FromMinutes(5));

        HorseResult result4 = await client.Cache.SetString("lim-3", "c", TimeSpan.FromMinutes(5));
        Assert.Equal(HorseResultCode.LimitExceeded, result4.Code);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_List_FilterEndsWith()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);
        await client.Cache.Purge();

        await client.Cache.SetString("user:online", "a", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("session:online", "b", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("user:offline", "c", TimeSpan.FromMinutes(5));

        var listResult2 = await client.Cache.List("*online");
        Assert.Equal(HorseResultCode.Ok, listResult2.Result.Code);
        Assert.Equal(2, listResult2.Model.Count);
        Assert.All(listResult2.Model, info => Assert.EndsWith("online", info.Key));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_List_FilterContains()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);
        await client.Cache.Purge();

        await client.Cache.SetString("my-user-key", "a", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("admin-user-data", "b", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("order-item", "c", TimeSpan.FromMinutes(5));

        var listResult3 = await client.Cache.List("*user*");
        Assert.Equal(HorseResultCode.Ok, listResult3.Result.Code);
        Assert.Equal(2, listResult3.Model.Count);
        Assert.All(listResult3.Model, info => Assert.Contains("user", info.Key));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_List_FilterExactMatch()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);
        await client.Cache.Purge();

        await client.Cache.SetString("exact-key", "a", TimeSpan.FromMinutes(5));
        await client.Cache.SetString("other-key", "b", TimeSpan.FromMinutes(5));

        var listResult4 = await client.Cache.List("exact-key");
        Assert.Equal(HorseResultCode.Ok, listResult4.Result.Code);
        Assert.Single(listResult4.Model);
        Assert.Equal("exact-key", listResult4.Model[0].Key);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetData_ResponseMetadata_HasExpiration()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);

        byte[] data2 = [10, 20, 30];
        await client.Cache.SetData("data-meta", data2, TimeSpan.FromMinutes(5));

        var result5 = await client.Cache.GetData("data-meta");
        Assert.NotNull(result5);
        Assert.Equal("data-meta", result5.Key);
        Assert.True(result5.Expiration > 0);
        Assert.Equal(data2, result5.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetData_WithWarningDuration()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        HorseClient client = await Connect(ctx.Port);

        byte[] data3 = [1, 2, 3];
        HorseResult setResult2 = await client.Cache.SetData(
            "data-warn", data3,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5));

        Assert.Equal(HorseResultCode.Ok, setResult2.Code);

        var result6 = await client.Cache.GetData("data-warn");
        Assert.NotNull(result6);

        client.Disconnect();
    }
}
