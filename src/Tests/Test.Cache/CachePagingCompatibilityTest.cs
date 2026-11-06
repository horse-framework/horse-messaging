using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;
using Xunit;

namespace Test.Cache;

public class CachePagingCompatibilityTest
{
    [Fact]
    public async Task NewServer_AcknowledgesPage_AndClientDoesNotSkipTwice()
    {
        await using var context = await CacheTestServer.Create();
        foreach (string key in new[] { "d", "b", "a", "c", "e" })
            context.Rider.Cache.Set(key, "value", TimeSpan.FromMinutes(5));
        var client = await Connect(context.Port);
        try
        {
            var page = await client.Cache.List(2, 2, CancellationToken.None);
            Assert.Equal(new[] { "c", "d" }, page.Model.Select(x => x.Key));
            Assert.Equal("2", page.Result.Message.FindHeader(HorseHeaders.CACHE_OFFSET));
            Assert.Equal("2", page.Result.Message.FindHeader(HorseHeaders.CACHE_LIMIT));

            var tail = await client.Cache.List(3, 0, CancellationToken.None);
            Assert.Equal(new[] { "d", "e" }, tail.Model.Select(x => x.Key));
            Assert.Equal("0", tail.Result.Message.FindHeader(HorseHeaders.CACHE_LIMIT));
        }
        finally { client.Disconnect(); }
    }

    [Theory]
    [InlineData(0, 2, new[] { "a", "b" })]
    [InlineData(1, 2, new[] { "b", "c" })]
    [InlineData(2, 0, new[] { "c", "d" })]
    [InlineData(100, 2, new string[0])]
    public async Task LegacyServer_IgnoresPagingHeaders_ClientAppliesSortedPage(int offset, int limit, string[] expected)
    {
        await using var context = await CacheTestServer.Create();
        var legacy = InstallLegacyHandler(context, ["d", "b", "a", "c"]);
        var client = await Connect(context.Port);
        try
        {
            var page = await client.Cache.List(offset, limit, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, page.Result.Code);
            Assert.Equal(expected, page.Model.Select(x => x.Key));
            Assert.Null(page.Result.Message.FindHeader(HorseHeaders.CACHE_OFFSET));
            Assert.Equal(new[] { "d", "b", "a", "c" }, legacy.Entries.Select(x => x.Key));
        }
        finally { client.Disconnect(); }
    }

    [Fact]
    public async Task LegacyServer_FilteredPage_AndWildcardRemainCompatible()
    {
        await using var context = await CacheTestServer.Create();
        var legacy = InstallLegacyHandler(context, ["other", "match-c", "match-a", "match-b"]);
        var client = await Connect(context.Port);
        try
        {
            var filtered = await client.Cache.List("match-*", 1, 1, CancellationToken.None);
            Assert.Equal("match-b", Assert.Single(filtered.Model).Key);
            Assert.Equal("match-*", legacy.LastFilter);

            var wildcard = await client.Cache.List("*", 1, 2, CancellationToken.None);
            Assert.Equal(new[] { "match-b", "match-c" }, wildcard.Model.Select(x => x.Key));
            Assert.Null(legacy.LastFilter);

            var unpaged = await client.Cache.List(CancellationToken.None);
            Assert.Equal(legacy.Entries.Select(x => x.Key), unpaged.Model.Select(x => x.Key));
        }
        finally { client.Disconnect(); }
    }

    [Fact]
    public async Task LegacyServer_Failure_IsPreservedWithoutPaging()
    {
        await using var context = await CacheTestServer.Create();
        var legacy = InstallLegacyHandler(context, ["a", "b"]);
        legacy.Fail = true;
        var client = await Connect(context.Port);
        try
        {
            var failure = await client.Cache.List(1, 2, CancellationToken.None);
            Assert.Equal(HorseResultCode.Failed, failure.Result.Code);
            Assert.Null(failure.Model);
            Assert.NotNull(failure.Result.Message);
        }
        finally { client.Disconnect(); }
    }

    [Fact]
    public async Task UnconfirmedPartialAcknowledgment_DoesNotDisableLegacyFallback()
    {
        await using var context = await CacheTestServer.Create();
        var legacy = InstallLegacyHandler(context, ["d", "b", "a", "c"]);
        legacy.EchoOffsetOnly = true;
        var client = await Connect(context.Port);
        try
        {
            var page = await client.Cache.List(1, 2, CancellationToken.None);
            Assert.Equal(new[] { "b", "c" }, page.Model.Select(x => x.Key));
        }
        finally { client.Disconnect(); }
    }

    private static LegacyListHandler InstallLegacyHandler(CacheTestContext context, string[] keys)
    {
        var legacy = new LegacyListHandler(keys);
        var protocol = (HorseProtocol)context.Rider.Server.FindProtocol("horse");
        object network = protocol.GetHandler();
        network.GetType().GetField("_cacheHandler", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(network, legacy);
        return legacy;
    }

    private static async Task<HorseClient> Connect(int port)
    {
        var client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{port}");
        return client;
    }

    // The TCP/handshake/response tracker remains real; only the cache handler
    // emulates the old server's full-list response and ignored paging headers.
    private sealed class LegacyListHandler(string[] keys) : INetworkMessageHandler
    {
        internal readonly List<CacheInformation> Entries = keys.Select(key => new CacheInformation { Key = key }).ToList();
        internal string LastFilter;
        internal bool Fail;
        internal bool EchoOffsetOnly;

        public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            LastFilter = message.FindHeader(HorseHeaders.FILTER);
            if (Fail || LastFilter == "*")
            {
                await client.SendAsync(message.CreateResponse(HorseResultCode.Failed));
                return;
            }

            List<CacheInformation> result = string.IsNullOrEmpty(LastFilter)
                ? Entries
                : Entries.Where(item => item.Key.StartsWith(LastFilter.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)).ToList();
            var response = message.CreateResponse(HorseResultCode.Ok);
            response.ContentType = KnownContentTypes.ChannelList;
            if (EchoOffsetOnly)
                response.AddHeader(HorseHeaders.CACHE_OFFSET, message.FindHeader(HorseHeaders.CACHE_OFFSET));
            response.Serialize(result, new SystemJsonContentSerializer());
            await client.SendAsync(response);
        }
    }
}
