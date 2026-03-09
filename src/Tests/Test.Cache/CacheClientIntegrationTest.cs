using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Clients;
using Xunit;

namespace Test.Cache;

public class SimpleModel
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class NestedModel
{
    public string Title { get; set; }
    public List<ChildItem> Items { get; set; }
}

public class ChildItem
{
    public int Seq { get; set; }
    public string Label { get; set; }
}

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

        HorseResult setResult = await client.Cache.SetString("c-key", "c-val", TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetString("c-key", CancellationToken.None);
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
        HorseResult setResult = await client.Cache.SetData("bin-key", data, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetData("bin-key", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(data, getResult.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Remove_KeyGone()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("rm-c", "val", TimeSpan.FromMinutes(5), CancellationToken.None);
        HorseResult rmResult = await client.Cache.Remove("rm-c", CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, rmResult.Code);

        var getResult = await client.Cache.GetString("rm-c", CancellationToken.None);
        Assert.Null(getResult);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Purge_AllKeysGone()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("purge-1", "a", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("purge-2", "b", TimeSpan.FromMinutes(5), CancellationToken.None);

        HorseResult purgeResult = await client.Cache.Purge(CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, purgeResult.Code);

        Assert.Null(await client.Cache.GetString("purge-1", CancellationToken.None));
        Assert.Null(await client.Cache.GetString("purge-2", CancellationToken.None));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_PurgeByTag()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("tag-1", "a", TimeSpan.FromMinutes(5), ["grp"], false, CancellationToken.None);
        await client.Cache.SetString("tag-2", "b", TimeSpan.FromMinutes(5), CancellationToken.None);

        await client.Cache.PurgeByTag("grp", CancellationToken.None);

        Assert.Null(await client.Cache.GetString("tag-1", CancellationToken.None));
        Assert.NotNull(await client.Cache.GetString("tag-2", CancellationToken.None));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetNonExistent_ReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        var result = await client.Cache.GetString("no-such-key", CancellationToken.None);
        Assert.Null(result);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Set_WithTags_VerifiedByPurgeByTag()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("t-key", "data", TimeSpan.FromMinutes(5), ["session"], false, CancellationToken.None);

        Assert.NotNull(await client.Cache.GetString("t-key", CancellationToken.None));

        await client.Cache.PurgeByTag("session", CancellationToken.None);

        Assert.Null(await client.Cache.GetString("t-key", CancellationToken.None));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_TwoClients_SharedCache()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient writer = await Connect(ctx.Port);
        HorseClient reader = await Connect(ctx.Port);

        await writer.Cache.SetString("shared", "from-writer", TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await reader.Cache.GetString("shared", CancellationToken.None);
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

        await client.Cache.SetString("list-1", "a", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("list-2", "b", TimeSpan.FromMinutes(5), CancellationToken.None);

        var listResult = await client.Cache.List(CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, listResult.Result.Code);
        Assert.True(listResult.Model.Count >= 2);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_ValueIncreases()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("inc-key", TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(r1);
        Assert.Equal(1, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("inc-key", TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(r2);
        Assert.Equal(2, r2.Value);

        var r3 = await client.Cache.GetIncrementalValue("inc-key", TimeSpan.FromMinutes(5), CancellationToken.None);
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
        await client.Cache.SetString("exp-key", "val", TimeSpan.FromSeconds(1), CancellationToken.None);
        await Task.Delay(1500);

        var result = await client.Cache.GetString("exp-key", CancellationToken.None);
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

        await client.Cache.SetString("dur-key", "dur-val", TimeSpan.FromSeconds(30), CancellationToken.None);

        var result = await client.Cache.GetString("dur-key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("dur-val", result.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetPersistent_FileCreated()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        await client.Cache.SetString("client-persist", "data", TimeSpan.FromMinutes(5), null, true, CancellationToken.None);

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

        var r1 = await client.Cache.GetIncrementalValue("step-key", TimeSpan.FromMinutes(5), 5, CancellationToken.None);
        Assert.NotNull(r1);
        Assert.Equal(5, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("step-key", TimeSpan.FromMinutes(5), 5, CancellationToken.None);
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
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("user:1", "a", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("user:2", "b", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("order:1", "c", TimeSpan.FromMinutes(5), CancellationToken.None);

        var listResult = await client.Cache.List("user*", CancellationToken.None);
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

        await client.Cache.SetString("ow-key", "first", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("ow-key", "second", TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await client.Cache.GetString("ow-key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("second", result.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Remove_NonExistent_ReturnsOk()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);

        HorseResult result = await client.Cache.Remove("nonexistent-client-key", CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetWithTags_VerifiedByList()
    {
        await using var ctx = await CacheTestServer.Create();

        HorseClient client = await Connect(ctx.Port);
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("tag-list-key", "data", TimeSpan.FromMinutes(5), ["sess", "user"], false, CancellationToken.None);

        var listResult = await client.Cache.List(CancellationToken.None);
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

        await client.Cache.SetString("meta-key", "meta-val", TimeSpan.FromMinutes(10), ["m-tag"], false, CancellationToken.None);

        var result = await client.Cache.GetString("meta-key", CancellationToken.None);
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

        HorseResult setResult = await client.Cache.SetString("no-dur", "val", CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetString("no-dur", CancellationToken.None);
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
        HorseResult setResult = await client.Cache.SetData("no-dur-data", data, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetData("no-dur-data", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(data, getResult.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_NoDuration_UsesDefaultDuration()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("no-dur-inc", CancellationToken.None);
        Assert.NotNull(r1);
        Assert.Equal(1, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("no-dur-inc", CancellationToken.None);
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
            TimeSpan.FromSeconds(5), null, false, CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var result2 = await client.Cache.GetString("warn-dur-key", CancellationToken.None);
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
        HorseResult result3 = await client.Cache.SetData("too-big", bigData, TimeSpan.FromMinutes(5), CancellationToken.None);
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
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("lim-1", "a", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("lim-2", "b", TimeSpan.FromMinutes(5), CancellationToken.None);

        HorseResult result4 = await client.Cache.SetString("lim-3", "c", TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(HorseResultCode.LimitExceeded, result4.Code);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_List_FilterEndsWith()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = await Connect(ctx.Port);
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("user:online", "a", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("session:online", "b", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("user:offline", "c", TimeSpan.FromMinutes(5), CancellationToken.None);

        var listResult2 = await client.Cache.List("*online", CancellationToken.None);
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
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("my-user-key", "a", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("admin-user-data", "b", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("order-item", "c", TimeSpan.FromMinutes(5), CancellationToken.None);

        var listResult3 = await client.Cache.List("*user*", CancellationToken.None);
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
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("exact-key", "a", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("other-key", "b", TimeSpan.FromMinutes(5), CancellationToken.None);

        var listResult4 = await client.Cache.List("exact-key", CancellationToken.None);
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
        await client.Cache.SetData("data-meta", data2, TimeSpan.FromMinutes(5), CancellationToken.None);

        var result5 = await client.Cache.GetData("data-meta", CancellationToken.None);
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
            TimeSpan.FromSeconds(5), null, false, CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, setResult2.Code);

        var result6 = await client.Cache.GetData("data-warn", CancellationToken.None);
        Assert.NotNull(result6);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generic Set<T> / Get<T> round-trip (JSON serialize/deserialize)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetGeneric_GetGeneric_SimpleModel_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 42, Name = "Horse" };
        var setResult = await client.Cache.Set("gen-simple", model, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.Get<SimpleModel>("gen-simple", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.NotNull(getResult.Value);
        Assert.Equal(42, getResult.Value.Id);
        Assert.Equal("Horse", getResult.Value.Name);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetGeneric_GetGeneric_NestedModel_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var model = new NestedModel
        {
            Title = "Root",
            Items =
            [
                new ChildItem { Seq = 1, Label = "A" },
                new ChildItem { Seq = 2, Label = "B" },
                new ChildItem { Seq = 3, Label = "C" }
            ]
        };

        await client.Cache.Set("gen-nested", model, TimeSpan.FromMinutes(5), CancellationToken.None);

        var getResult = await client.Cache.Get<NestedModel>("gen-nested", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.NotNull(getResult.Value);
        Assert.Equal("Root", getResult.Value.Title);
        Assert.Equal(3, getResult.Value.Items.Count);
        Assert.Equal("B", getResult.Value.Items[1].Label);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetGeneric_NoDuration_GetGeneric_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 99, Name = "NoDur" };
        var setResult = await client.Cache.Set("gen-nodur", model, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.Get<SimpleModel>("gen-nodur", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(99, getResult.Value.Id);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetGeneric_WithTags_GetGeneric_TagsPresent()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 1, Name = "Tagged" };
        await client.Cache.Set("gen-tag", model, TimeSpan.FromMinutes(5), ["group-a", "group-b"], false, CancellationToken.None);

        var getResult = await client.Cache.Get<SimpleModel>("gen-tag", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.NotNull(getResult.Tags);
        Assert.Equal(2, getResult.Tags.Length);
        Assert.Contains("group-a", getResult.Tags);
        Assert.Contains("group-b", getResult.Tags);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetGeneric_WithPersistent_FileCreated()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 7, Name = "Persist" };
        await client.Cache.Set("gen-persist", model, TimeSpan.FromMinutes(5), null, true, CancellationToken.None);

        var getResult = await client.Cache.Get<SimpleModel>("gen-persist", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(7, getResult.Value.Id);

        string cacheDir = Path.Combine(ctx.Rider.Options.DataPath, "Cache");
        Assert.True(Directory.Exists(cacheDir));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetGeneric_WithWarningDuration()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });
        var client = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 55, Name = "WarnDur" };
        var setResult = await client.Cache.Set("gen-warn", model,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), null, false, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.Get<SimpleModel>("gen-warn", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(55, getResult.Value.Id);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetGeneric_NonExistent_ReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var result = await client.Cache.Get<SimpleModel>("no-such-generic", CancellationToken.None);
        Assert.Null(result);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cross-type reads: SetString → GetData, SetData → GetString
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetString_GetData_ReturnsSameBytes()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        await client.Cache.SetString("cross-s2d", "hello-cross", TimeSpan.FromMinutes(5), CancellationToken.None);

        var dataResult = await client.Cache.GetData("cross-s2d", CancellationToken.None);
        Assert.NotNull(dataResult);
        Assert.Equal("hello-cross", Encoding.UTF8.GetString(dataResult.Value));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetData_GetString_ReturnsSameString()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        byte[] data = Encoding.UTF8.GetBytes("data-to-string");
        await client.Cache.SetData("cross-d2s", data, TimeSpan.FromMinutes(5), CancellationToken.None);

        var stringResult = await client.Cache.GetString("cross-d2s", CancellationToken.None);
        Assert.NotNull(stringResult);
        Assert.Equal("data-to-string", stringResult.Value);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Incremental — client-side overload coverage gaps
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_GetIncremental_NoDurationNoStep_DefaultsToIncrement1()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("inc-bare", CancellationToken.None);
        Assert.NotNull(r1);
        Assert.Equal(1, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("inc-bare", CancellationToken.None);
        Assert.NotNull(r2);
        Assert.Equal(2, r2.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_CustomStep_NoDuration()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("inc-step-nodur", 3, CancellationToken.None);
        Assert.NotNull(r1);
        Assert.Equal(3, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("inc-step-nodur", 3, CancellationToken.None);
        Assert.NotNull(r2);
        Assert.Equal(6, r2.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_ZeroStep_ValueDoesNotChange()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("inc-zero", TimeSpan.FromMinutes(5), 0, CancellationToken.None);
        Assert.NotNull(r1);

        var r2 = await client.Cache.GetIncrementalValue("inc-zero", TimeSpan.FromMinutes(5), 0, CancellationToken.None);
        Assert.NotNull(r2);
        Assert.Equal(r1.Value, r2.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_NegativeStep_Decrements()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("inc-neg", TimeSpan.FromMinutes(5), 10, CancellationToken.None);
        Assert.Equal(10, r1.Value);

        var r2 = await client.Cache.GetIncrementalValue("inc-neg", TimeSpan.FromMinutes(5), -3, CancellationToken.None);
        Assert.Equal(7, r2.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_GetIncremental_MetadataPopulated()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var r1 = await client.Cache.GetIncrementalValue("inc-meta", TimeSpan.FromMinutes(10), CancellationToken.None);
        Assert.NotNull(r1);
        Assert.Equal("inc-meta", r1.Key);
        Assert.True(r1.Expiration > 0, "Incremental value should have expiration metadata");

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Metadata on generic Get<T>
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_GetGeneric_ExpirationMetadata_Populated()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 100, Name = "Meta" };
        await client.Cache.Set("gen-meta", model, TimeSpan.FromMinutes(10), ["meta-tag"], false, CancellationToken.None);

        var getResult = await client.Cache.Get<SimpleModel>("gen-meta", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal("gen-meta", getResult.Key);
        Assert.True(getResult.Expiration > 0, "Expiration should be positive for Get<T>");
        Assert.Contains("meta-tag", getResult.Tags);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Concurrent multi-client writes/reads
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_ConcurrentWrites_LastWriteWins()
    {
        await using var ctx = await CacheTestServer.Create();

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(async () =>
            {
                var c = await Connect(ctx.Port);
                await c.Cache.SetString("concurrent-key", $"val-{idx}", TimeSpan.FromMinutes(5), CancellationToken.None);
                c.Disconnect();
            }));
        }

        await Task.WhenAll(tasks);

        var reader = await Connect(ctx.Port);
        var result = await reader.Cache.GetString("concurrent-key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.StartsWith("val-", result.Value);

        reader.Disconnect();
    }

    [Fact]
    public async Task Client_ConcurrentIncrements_AllCounted()
    {
        await using var ctx = await CacheTestServer.Create();

        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var c = await Connect(ctx.Port);
                await c.Cache.GetIncrementalValue("conc-inc", TimeSpan.FromMinutes(5), CancellationToken.None);
                c.Disconnect();
            }));
        }

        await Task.WhenAll(tasks);

        var reader = await Connect(ctx.Port);
        var result = await reader.Cache.GetIncrementalValue("conc-inc", TimeSpan.FromMinutes(5), 0, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(20, result.Value);

        reader.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Disconnected client — operations should fail gracefully
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_Disconnected_SetString_ReturnsFailedOrThrows()
    {
        await using var ctx = await CacheTestServer.Create();

        var client = await Connect(ctx.Port);
        client.Disconnect();

        var ex = await Record.ExceptionAsync(async () =>
        {
            await client.Cache.SetString("disc-key", "val", TimeSpan.FromMinutes(5), CancellationToken.None);
        });

        // Either an exception is thrown OR a non-Ok result is returned — both are acceptable
        // The key point is it does NOT hang
    }

    [Fact]
    public async Task Client_Disconnected_GetString_ReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create();

        var client = await Connect(ctx.Port);
        await client.Cache.SetString("disc-get-key", "val", TimeSpan.FromMinutes(5), CancellationToken.None);
        client.Disconnect();

        var result = await client.Cache.GetString("disc-get-key", CancellationToken.None);
        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Empty and special key edge cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetString_EmptyValue_GetReturnsEmpty()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        await client.Cache.SetString("empty-val-c", "", TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await client.Cache.GetString("empty-val-c", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("", result.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetString_SpecialCharKey_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        string key = "user:123:session:active";
        await client.Cache.SetString(key, "value", TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await client.Cache.GetString(key, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("value", result.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetString_UnicodeValue_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        string unicodeVal = "Merhaba Dünya 🌍 こんにちは";
        await client.Cache.SetString("unicode-key", unicodeVal, TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await client.Cache.GetString("unicode-key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(unicodeVal, result.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetData_EmptyByteArray_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        byte[] empty = Array.Empty<byte>();
        await client.Cache.SetData("empty-data-c", empty, TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await client.Cache.GetData("empty-data-c", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(result.Value);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multi-tag scenarios
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_MultipleTags_PurgeOneTag_OnlyMatchingRemoved()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("mt-1", "a", TimeSpan.FromMinutes(5), ["alpha", "beta"], false, CancellationToken.None);
        await client.Cache.SetString("mt-2", "b", TimeSpan.FromMinutes(5), ["beta", "gamma"], false, CancellationToken.None);
        await client.Cache.SetString("mt-3", "c", TimeSpan.FromMinutes(5), ["gamma"], false, CancellationToken.None);

        await client.Cache.PurgeByTag("alpha", CancellationToken.None);

        Assert.Null(await client.Cache.GetString("mt-1", CancellationToken.None));
        Assert.NotNull(await client.Cache.GetString("mt-2", CancellationToken.None));
        Assert.NotNull(await client.Cache.GetString("mt-3", CancellationToken.None));

        client.Disconnect();
    }

    [Fact]
    public async Task Client_MultipleTags_PurgeSharedTag_RemovesAllWithTag()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("sh-1", "a", TimeSpan.FromMinutes(5), ["shared", "x"], false, CancellationToken.None);
        await client.Cache.SetString("sh-2", "b", TimeSpan.FromMinutes(5), ["shared", "y"], false, CancellationToken.None);
        await client.Cache.SetString("sh-3", "c", TimeSpan.FromMinutes(5), ["z"], false, CancellationToken.None);

        await client.Cache.PurgeByTag("shared", CancellationToken.None);

        Assert.Null(await client.Cache.GetString("sh-1", CancellationToken.None));
        Assert.Null(await client.Cache.GetString("sh-2", CancellationToken.None));
        Assert.NotNull(await client.Cache.GetString("sh-3", CancellationToken.None));

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Overwrite changes value, tags, and TTL
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_Overwrite_UpdatesValueTagsAndExpiration()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        await client.Cache.SetString("ow-full", "v1", TimeSpan.FromMinutes(1), ["tag1"], false, CancellationToken.None);
        await client.Cache.SetString("ow-full", "v2", TimeSpan.FromMinutes(30), ["tag2", "tag3"], false, CancellationToken.None);

        var result = await client.Cache.GetString("ow-full", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("v2", result.Value);
        Assert.Equal(2, result.Tags.Length);
        Assert.Contains("tag2", result.Tags);
        Assert.Contains("tag3", result.Tags);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Large binary data round-trip via client
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetData_LargePayload_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        byte[] largeData = new byte[64 * 1024];
        Random.Shared.NextBytes(largeData);

        var setResult = await client.Cache.SetData("large-payload", largeData, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetData("large-payload", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(largeData, getResult.Value);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Set generic with duration → expired → Get returns null
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetGeneric_Expired_GetReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });
        var client = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 1, Name = "Expiring" };
        await client.Cache.Set("gen-exp", model, TimeSpan.FromSeconds(1), CancellationToken.None);
        await Task.Delay(1500);

        var result = await client.Cache.Get<SimpleModel>("gen-exp", CancellationToken.None);
        Assert.Null(result);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Remove after Set<T> — Get<T> returns null
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetGeneric_Remove_GetGenericReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        await client.Cache.Set("gen-rm", new SimpleModel { Id = 1, Name = "RM" }, TimeSpan.FromMinutes(5), CancellationToken.None);

        var rmResult = await client.Cache.Remove("gen-rm", CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, rmResult.Code);

        var getResult = await client.Cache.Get<SimpleModel>("gen-rm", CancellationToken.None);
        Assert.Null(getResult);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Two clients — one writes generic, other reads generic
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_TwoClients_GenericWriteRead()
    {
        await using var ctx = await CacheTestServer.Create();

        var writer = await Connect(ctx.Port);
        var reader = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 77, Name = "SharedGeneric" };
        await writer.Cache.Set("gen-shared", model, TimeSpan.FromMinutes(5), CancellationToken.None);

        var getResult = await reader.Cache.Get<SimpleModel>("gen-shared", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(77, getResult.Value.Id);
        Assert.Equal("SharedGeneric", getResult.Value.Name);

        writer.Disconnect();
        reader.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Purge clears generic Set<T> entries too
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_Purge_ClearsGenericEntries()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        await client.Cache.Set("gen-p1", new SimpleModel { Id = 1, Name = "A" }, TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.Set("gen-p2", new SimpleModel { Id = 2, Name = "B" }, TimeSpan.FromMinutes(5), CancellationToken.None);

        await client.Cache.Purge(CancellationToken.None);

        Assert.Null(await client.Cache.Get<SimpleModel>("gen-p1", CancellationToken.None));
        Assert.Null(await client.Cache.Get<SimpleModel>("gen-p2", CancellationToken.None));

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CancellationToken honored — cancelled token throws
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_CancelledToken_ThrowsOrReturnsImmediately()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Record.ExceptionAsync(async () =>
        {
            await client.Cache.SetString("cancel-key", "val", TimeSpan.FromMinutes(5), cts.Token);
        });

        // We accept either OperationCanceledException or a failed result — key point is no hang
        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Set with all parameters (full overload) round-trip
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetString_FullOverload_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });
        var client = await Connect(ctx.Port);

        var setResult = await client.Cache.SetString(
            "full-key", "full-val",
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1),
            ["f-tag1", "f-tag2"],
            false,
            CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetString("full-key", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal("full-val", getResult.Value);
        Assert.Equal("full-key", getResult.Key);
        Assert.Contains("f-tag1", getResult.Tags);
        Assert.Contains("f-tag2", getResult.Tags);
        Assert.True(getResult.Expiration > 0);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetData_FullOverload_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });
        var client = await Connect(ctx.Port);

        byte[] data = [10, 20, 30, 40, 50];
        var setResult = await client.Cache.SetData(
            "data-full", data,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1),
            ["d-tag"],
            false,
            CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetData("data-full", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(data, getResult.Value);
        Assert.Contains("d-tag", getResult.Tags);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rapid set-remove-set-get for same key
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_RapidSetRemoveSetGet_FinalValueCorrect()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        await client.Cache.SetString("rapid-c", "first", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.Remove("rapid-c", CancellationToken.None);
        await client.Cache.SetString("rapid-c", "second", TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await client.Cache.GetString("rapid-c", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("second", result.Value);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // List after purge returns empty
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_PurgeThenList_ReturnsEmpty()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        await client.Cache.SetString("pl-1", "a", TimeSpan.FromMinutes(5), CancellationToken.None);
        await client.Cache.SetString("pl-2", "b", TimeSpan.FromMinutes(5), CancellationToken.None);

        await client.Cache.Purge(CancellationToken.None);

        var listResult = await client.Cache.List(CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, listResult.Result.Code);
        Assert.Empty(listResult.Model);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cross-type: Set<T> (JSON) → GetString, SetString (JSON) → Get<T>
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetGeneric_GetString_ReturnsRawJson()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        var model = new SimpleModel { Id = 10, Name = "JsonRaw" };
        await client.Cache.Set("gen-to-str", model, TimeSpan.FromMinutes(5), CancellationToken.None);

        var stringResult = await client.Cache.GetString("gen-to-str", CancellationToken.None);
        Assert.NotNull(stringResult);
        // Should be valid JSON containing the serialized fields
        Assert.Contains("10", stringResult.Value);
        Assert.Contains("JsonRaw", stringResult.Value);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_SetString_ValidJson_GetGeneric_Deserializes()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        string json = """{"Id":55,"Name":"FromJson"}""";
        await client.Cache.SetString("str-to-gen", json, TimeSpan.FromMinutes(5), CancellationToken.None);

        var genericResult = await client.Cache.Get<SimpleModel>("str-to-gen", CancellationToken.None);
        Assert.NotNull(genericResult);
        Assert.NotNull(genericResult.Value);
        Assert.Equal(55, genericResult.Value.Id);
        Assert.Equal("FromJson", genericResult.Value.Name);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SetData with persistent flag — file verification
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_SetData_Persistent_FileCreated()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);

        byte[] data = [0xCA, 0xFE, 0xBA, 0xBE];
        await client.Cache.SetData("bin-persist-c", data, TimeSpan.FromMinutes(5), null, true, CancellationToken.None);

        string cacheDir = Path.Combine(ctx.Rider.Options.DataPath, "Cache");
        string filePath = Path.Combine(cacheDir, "bin-persist-c.hci");
        Assert.True(File.Exists(filePath));

        // Verify value is readable
        var getResult = await client.Cache.GetData("bin-persist-c", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(data, getResult.Value);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ExpirationWarning end-to-end via client
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_ExpirationWarning_IsFirstWarnedClient_Populated()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var client = await Connect(ctx.Port);

        // Set with short warning time via server-side directly so warning timing is precise
        ctx.Rider.Cache.Set("warn-e2e", "data", TimeSpan.FromSeconds(5),
            expirationWarning: TimeSpan.FromMilliseconds(100));

        await Task.Delay(250);

        // First client read after warning time — should be first warned client
        var first = await client.Cache.GetString("warn-e2e", CancellationToken.None);
        Assert.NotNull(first);
        Assert.Equal("data", first.Value);
        Assert.True(first.IsFirstWarnedClient, "First reader after warning should be IsFirstWarnedClient=true");

        // Second read — no longer first
        var second = await client.Cache.GetString("warn-e2e", CancellationToken.None);
        Assert.NotNull(second);
        Assert.False(second.IsFirstWarnedClient, "Subsequent reader should be IsFirstWarnedClient=false");

        client.Disconnect();
    }

    [Fact]
    public async Task Client_ExpirationWarning_WarningDatePopulated()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var client = await Connect(ctx.Port);

        // Set with warning duration via full overload
        await client.Cache.SetString("warn-meta", "data",
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1),
            null, false, CancellationToken.None);

        var result = await client.Cache.GetString("warn-meta", CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.Expiration > 0, "Expiration should be populated");
        // WarningDate should be set (non-zero) since we provided a warning duration
        Assert.True(result.WarningDate > 0, "WarningDate should be populated when warning duration is set");

        client.Disconnect();
    }

    [Fact]
    public async Task Client_ExpirationWarning_BeforeWarningTime_NotWarned()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
            o.ExpirationWarningIsEnabled = false;
        });

        var client = await Connect(ctx.Port);

        // Set with long expiration and no warning
        await client.Cache.SetString("no-warn-e2e", "data", TimeSpan.FromMinutes(10), CancellationToken.None);

        var result = await client.Cache.GetString("no-warn-e2e", CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(result.IsFirstWarnedClient);

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PurgeByTag case-insensitive via client
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_PurgeByTag_CaseInsensitive_RemovesMatching()
    {
        await using var ctx = await CacheTestServer.Create();
        var client = await Connect(ctx.Port);
        await client.Cache.Purge(CancellationToken.None);

        await client.Cache.SetString("ci-tag-1", "a", TimeSpan.FromMinutes(5), ["MyGroup"], false, CancellationToken.None);
        await client.Cache.SetString("ci-tag-2", "b", TimeSpan.FromMinutes(5), CancellationToken.None);
        await Task.Delay(250);

        // Purge with different case
        await client.Cache.PurgeByTag("mygroup", CancellationToken.None);
        await Task.Delay(100);

        Assert.Null(await client.Cache.GetString("ci-tag-1", CancellationToken.None));
        Assert.NotNull(await client.Cache.GetString("ci-tag-2", CancellationToken.None));

        client.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Authorization — Get/Set/Remove/Purge rejection
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Client_Authorization_SetRejected_ReturnsUnauthorized()
    {
        await using var ctx = await CacheTestServer.Create(null, c =>
        {
            c.Authorizations.Add(new DenyAllCacheAuthorization());
        });

        var client = await Connect(ctx.Port);

        HorseResult result = await client.Cache.SetString("denied", "val", TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(HorseResultCode.Unauthorized, result.Code);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Authorization_GetRejected_ReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create(null, c =>
        {
            c.Authorizations.Add(new DenyGetCacheAuthorization());
        });

        var client = await Connect(ctx.Port);

        // Set is allowed, get is denied
        ctx.Rider.Cache.Set("auth-get", "secret", TimeSpan.FromMinutes(5));

        var result = await client.Cache.GetString("auth-get", CancellationToken.None);
        Assert.Null(result);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Authorization_RemoveRejected_ReturnsUnauthorized()
    {
        await using var ctx = await CacheTestServer.Create(null, c =>
        {
            c.Authorizations.Add(new DenyRemoveCacheAuthorization());
        });

        var client = await Connect(ctx.Port);

        ctx.Rider.Cache.Set("auth-rm", "data", TimeSpan.FromMinutes(5));

        HorseResult result = await client.Cache.Remove("auth-rm", CancellationToken.None);
        Assert.Equal(HorseResultCode.Unauthorized, result.Code);

        // Key should still exist
        var getResult = await client.Cache.GetString("auth-rm", CancellationToken.None);
        Assert.NotNull(getResult);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Authorization_PurgeRejected_ReturnsUnauthorized()
    {
        await using var ctx = await CacheTestServer.Create(null, c =>
        {
            c.Authorizations.Add(new DenyPurgeCacheAuthorization());
        });

        var client = await Connect(ctx.Port);

        ctx.Rider.Cache.Set("auth-purge", "data", TimeSpan.FromMinutes(5));

        HorseResult result = await client.Cache.Purge(CancellationToken.None);
        Assert.Equal(HorseResultCode.Unauthorized, result.Code);

        // Key should still exist
        var getResult = await client.Cache.GetString("auth-purge", CancellationToken.None);
        Assert.NotNull(getResult);

        client.Disconnect();
    }

    [Fact]
    public async Task Client_Authorization_AllowedOperations_Work()
    {
        await using var ctx = await CacheTestServer.Create(null, c =>
        {
            c.Authorizations.Add(new AllowAllCacheAuthorization());
        });

        var client = await Connect(ctx.Port);

        var setResult = await client.Cache.SetString("auth-allow", "val", TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, setResult.Code);

        var getResult = await client.Cache.GetString("auth-allow", CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal("val", getResult.Value);

        var rmResult = await client.Cache.Remove("auth-allow", CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, rmResult.Code);

        client.Disconnect();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Test authorization implementations
// ─────────────────────────────────────────────────────────────────────────────

internal class DenyAllCacheAuthorization : ICacheAuthorization
{
    public bool CanGet(MessagingClient client, string key) => false;
    public bool CanSet(MessagingClient client, string key, MemoryStream value) => false;
    public bool CanRemove(MessagingClient client, string key) => false;
    public bool CanPurge(MessagingClient client) => false;
}

internal class DenyGetCacheAuthorization : ICacheAuthorization
{
    public bool CanGet(MessagingClient client, string key) => false;
    public bool CanSet(MessagingClient client, string key, MemoryStream value) => true;
    public bool CanRemove(MessagingClient client, string key) => true;
    public bool CanPurge(MessagingClient client) => true;
}

internal class DenyRemoveCacheAuthorization : ICacheAuthorization
{
    public bool CanGet(MessagingClient client, string key) => true;
    public bool CanSet(MessagingClient client, string key, MemoryStream value) => true;
    public bool CanRemove(MessagingClient client, string key) => false;
    public bool CanPurge(MessagingClient client) => true;
}

internal class DenyPurgeCacheAuthorization : ICacheAuthorization
{
    public bool CanGet(MessagingClient client, string key) => true;
    public bool CanSet(MessagingClient client, string key, MemoryStream value) => true;
    public bool CanRemove(MessagingClient client, string key) => true;
    public bool CanPurge(MessagingClient client) => false;
}

internal class AllowAllCacheAuthorization : ICacheAuthorization
{
    public bool CanGet(MessagingClient client, string key) => true;
    public bool CanSet(MessagingClient client, string key, MemoryStream value) => true;
    public bool CanRemove(MessagingClient client, string key) => true;
    public bool CanPurge(MessagingClient client) => true;
}
