using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Cache;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Xunit;

namespace Test.Cache;

public class CacheLegacyPagingTest
{
    [Fact]
    public async Task OriginalInterfaceImplementation_DefaultPagingSortsWithoutMutatingSource()
    {
        LegacyCache legacy = new(["z", "a", "c", "b"]);
        IHorseCache cache = legacy;
        var page = await cache.List(1, 2, CancellationToken.None);
        var tail = await cache.List(2, 0, CancellationToken.None);
        Assert.Equal(new[] { "b", "c" }, page.Model.Select(x => x.Key));
        Assert.Equal(new[] { "c", "z" }, tail.Model.Select(x => x.Key));
        Assert.Equal(new[] { "z", "a", "c", "b" }, legacy.Entries.Select(x => x.Key));
    }

    [Fact]
    public async Task OriginalInterfaceImplementation_DefaultPagingUsesOriginalFilteredList()
    {
        LegacyCache legacy = new(["other", "match-c", "match-a", "match-b"]);
        IHorseCache cache = legacy;
        var page = await cache.List("match-*", 1, 1, CancellationToken.None);
        Assert.Equal("match-*", legacy.LastFilter);
        Assert.Equal("match-b", Assert.Single(page.Model).Key);
    }

    // Implements only the original abstract interface members. New page methods
    // must have defaults so existing third-party implementations remain valid.
    private sealed class LegacyCache(string[] keys) : IHorseCache
    {
        internal readonly List<CacheInformation> Entries = keys.Select(key => new CacheInformation { Key = key }).ToList();
        internal string LastFilter;
        public Task<HorseModelResult<List<CacheInformation>>> List(CancellationToken cancellationToken)
            => Task.FromResult(new HorseModelResult<List<CacheInformation>>(new HorseResult(HorseResultCode.Ok), Entries));
        public Task<HorseModelResult<List<CacheInformation>>> List(string filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;
            var matches = Entries.Where(x => x.Key.StartsWith(filter.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(new HorseModelResult<List<CacheInformation>>(new HorseResult(HorseResultCode.Ok), matches));
        }
        public Task<HorseCacheData<TData>> Get<TData>(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseCacheData<string>> GetString(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseCacheData<byte[]>> GetData(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseCacheData<int>> GetIncrementalValue(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, int increment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> Set<TData>(string key, TData data, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> Set<TData>(string key, TData data, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetString(string key, string data, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetString(string key, string data, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetString(string key, string data, TimeSpan duration, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetData(string key, byte[] data, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetData(string key, byte[] data, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> Remove(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> Purge(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HorseResult> PurgeByTag(string tag, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
