using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Clients;
using Xunit;

namespace Test.Cache;

public class CacheListingPerformanceTest
{
    [Fact]
    public async Task Client_List_AppliesFilterBeforePaging()
    {
        await using var ctx = await CacheTestServer.Create();
        for (int i = 0; i < 20; i++)
            ctx.Rider.Cache.Set($"other-{i}", "other", TimeSpan.FromMinutes(5));
        for (int i = 0; i < 8; i++)
            ctx.Rider.Cache.Set($"group-{i}", "match", TimeSpan.FromMinutes(5));

        HorseClient client = await Connect(ctx.Port);
        try
        {
            var matches = await client.Cache.List("group-*", CancellationToken.None);
            var page = await client.Cache.List("group-*", 2, 3, CancellationToken.None);
            var pastEnd = await client.Cache.List("group-*", 20, 3, CancellationToken.None);
            var all = await client.Cache.List("*", 0, 0, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, page.Result.Code);
            Assert.Equal(matches.Model.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Skip(2).Take(3).Select(x => x.Key), page.Model.Select(x => x.Key));
            Assert.Empty(pastEnd.Model);
            Assert.Equal(28, all.Model.Count);
        }
        finally
        {
            client.Disconnect();
        }
    }

    [Fact]
    public async Task Client_List_AuthorizesBeforePaging()
    {
        await using var ctx = await CacheTestServer.Create();
        for (int i = 0; i < 8; i++)
            ctx.Rider.Cache.Set($"private-{i}", "hidden", TimeSpan.FromMinutes(5));
        for (int i = 0; i < 5; i++)
            ctx.Rider.Cache.Set($"public-{i}", "visible", TimeSpan.FromMinutes(5));
        ctx.Rider.Cache.Authorizations.Add(new PublicKeysAuthorization());

        HorseClient client = await Connect(ctx.Port);
        try
        {
            var all = await client.Cache.List(CancellationToken.None);
            var page = await client.Cache.List(1, 2, CancellationToken.None);

            Assert.Equal(5, all.Model.Count);
            Assert.All(all.Model, item => Assert.StartsWith("public-", item.Key));
            Assert.Equal(all.Model.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Skip(1).Take(2).Select(x => x.Key), page.Model.Select(x => x.Key));
        }
        finally
        {
            client.Disconnect();
        }
    }

    [Fact]
    public async Task Client_Get_MetadataReflectsPropertyUpdates()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseCacheItem item = ctx.Rider.Cache.Set("metadata", "value", TimeSpan.FromMinutes(5), tags: ["old"]).Item;
        item.Tags = ["new", "second"];
        item.Expiration = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600).UtcDateTime;
        item.ExpirationWarning = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 540).UtcDateTime;

        HorseClient client = await Connect(ctx.Port);
        try
        {
            var result = await client.Cache.GetString("metadata", CancellationToken.None);
            Assert.Equal(new[] { "new", "second" }, result.Tags);
            Assert.Equal(new DateTimeOffset(item.Expiration).ToUnixTimeSeconds(), result.Expiration);
            Assert.Equal(new DateTimeOffset(item.ExpirationWarning.Value).ToUnixTimeSeconds(), result.WarningDate);

            item.Tags = [];
            item.ExpirationWarning = null;
            var cleared = await client.Cache.GetString("metadata", CancellationToken.None);
            Assert.Empty(cleared.Tags);
            Assert.Equal(0, cleared.WarningDate);
        }
        finally
        {
            client.Disconnect();
        }
    }

    [Fact]
    public async Task Client_InvalidPage_ReceivesFailureAndConnectionRemainsUsable()
    {
        await using var ctx = await CacheTestServer.Create();
        ctx.Rider.Cache.Set("alive", "value", TimeSpan.FromMinutes(5));
        HorseClient client = await Connect(ctx.Port);
        try
        {
            var request = new HorseMessage(MessageType.Cache, null, KnownContentTypes.GetCacheList);
            request.AddHeader(HorseHeaders.CACHE_LIMIT, "-1");
            var invalid = await client.SendAsync(request, true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Failed, invalid.Code);
            Assert.Equal("value", (await client.Cache.GetString("alive", CancellationToken.None)).Value);
        }
        finally
        {
            client.Disconnect();
        }
    }

    [Fact]
    public void Client_List_RejectsNegativePagingValues()
    {
        HorseClient client = new HorseClient();
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = client.Cache.List(-1, 5); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = client.Cache.List("*", 0, -1); });
    }

    [Fact]
    public async Task Item_TagsAreOwnedAndSetterUpdatesPurgeIndex()
    {
        await using var ctx = await CacheTestServer.Create();
        string[] input = ["old"];
        HorseCacheItem item = ctx.Rider.Cache.Set("retagged", "value", TimeSpan.FromMinutes(5), tags: input).Item;
        input[0] = "caller-change";
        string[] returned = item.Tags;
        returned[0] = "reader-change";
        Assert.Equal(new[] { "old" }, item.Tags);

        item.Tags = ["new"];
        ctx.Rider.Cache.PurgeByTag("old");
        Assert.True(ctx.Rider.Cache.TryGet("retagged", out _));
        ctx.Rider.Cache.PurgeByTag("new");
        Assert.False(ctx.Rider.Cache.TryGet("retagged", out _));
    }

    [Fact]
    public async Task Item_ExpirationSetterReschedulesAnEntryBehindAnotherDeadline()
    {
        await using var ctx = await CacheTestServer.Create();
        const string key = "rescheduled-expiration";
        // Keep a later live deadline at the heap root before shortening this
        // entry's lifetime. Both keys use the same cache partition.
        uint partition = (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(key) % 64;
        string blocker = Enumerable.Range(0, 10000).Select(i => "deadline-blocker-" + i)
            .First(candidate => (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(candidate) % 64 == partition);
        ctx.Rider.Cache.Set(blocker, "live", TimeSpan.FromMinutes(1));
        HorseCacheItem item = ctx.Rider.Cache.Set(key, "expire", TimeSpan.FromMinutes(5)).Item;
        item.Expiration = DateTime.UtcNow.AddSeconds(-1);

        var keys = ctx.Rider.Cache.GetCacheKeys();
        Assert.Single(keys);
        Assert.Equal(blocker, keys[0].Key);
        Assert.Equal(1, ctx.Rider.Cache.Count);
    }

    [Fact]
    public async Task Item_DetachedMetadataChangesDoNotAffectReplacement()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseCacheItem old = ctx.Rider.Cache.Set("replaced", "old", TimeSpan.FromMinutes(5), tags: ["old-tag"]).Item;
        ctx.Rider.Cache.Set("replaced", "new", TimeSpan.FromMinutes(5), tags: ["new-tag"]);
        old.Tags = ["ghost-tag"];
        old.Expiration = DateTime.UtcNow.AddSeconds(-1);

        Assert.Single(ctx.Rider.Cache.GetCacheKeys());
        ctx.Rider.Cache.PurgeByTag("ghost-tag");
        ctx.Rider.Cache.PurgeByTag("old-tag");
        Assert.True(ctx.Rider.Cache.TryGet("replaced", out var current));
        Assert.Equal("new", System.Text.Encoding.UTF8.GetString(current.Item.Value.ToArray()));
        ctx.Rider.Cache.PurgeByTag("new-tag");
        Assert.False(ctx.Rider.Cache.TryGet("replaced", out _));
    }

    [Fact]
    public async Task Item_PublishedKeyCanOnlyChangeCasing()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseCacheItem item = ctx.Rider.Cache.Set("stable", "value", TimeSpan.FromMinutes(5)).Item;
        item.Key = "STABLE";
        Assert.Throws<InvalidOperationException>(() => item.Key = "renamed");
        Assert.True(ctx.Rider.Cache.TryGet("stable", out _));
        Assert.False(ctx.Rider.Cache.TryGet("renamed", out _));
    }

    private static async Task<HorseClient> Connect(int port)
    {
        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{port}");
        return client;
    }

    private sealed class PublicKeysAuthorization : ICacheAuthorization
    {
        public bool CanGet(MessagingClient client, string key) => key.StartsWith("public-", StringComparison.Ordinal);
        public bool CanSet(MessagingClient client, string key, System.IO.MemoryStream value) => true;
        public bool CanRemove(MessagingClient client, string key) => true;
        public bool CanPurge(MessagingClient client) => true;
    }
}
