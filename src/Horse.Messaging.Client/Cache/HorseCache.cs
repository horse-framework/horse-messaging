using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client.Cache;

internal class HorseCache<TIdentifier> : HorseCache, IHorseCache<TIdentifier>
{
    internal HorseCache(HorseClient client) : base(client) { }
}

internal class HorseCache : IHorseCache
{
    private readonly HorseClient _client;

    internal HorseCache(HorseClient client)
    {
        _client = client;
    }

    #region Get

    public async Task<HorseCacheData<TData>> Get<TData>(string key, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
        HorseModelResult<TData> result = await _client.SendAsync<TData>(message, cancellationToken);

        if (result == null || result.Result?.Code != HorseResultCode.Ok || result.Result.Message == null)
            return null;

        HorseCacheData<TData> data = CreateCacheData<TData>(key, result.Result.Message);
        data.Value = result.Model;
        return data;
    }

    public async Task<HorseCacheData<string>> GetString(string key, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
        HorseResult result = await _client.SendAsync(message, true, cancellationToken);

        if (result == null || result.Code != HorseResultCode.Ok)
            return null;

        HorseCacheData<string> data = CreateCacheData<string>(key, result.Message);
        data.Value = result.Message.Content != null && result.Message.Length > 0
            ? result.Message.GetStringContent()
            : string.Empty;
        return data;
    }

    public async Task<HorseCacheData<byte[]>> GetData(string key, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
        HorseResult result = await _client.SendAsync(message, true, cancellationToken);

        if (result == null || result.Code != HorseResultCode.Ok)
            return null;

        HorseCacheData<byte[]> data = CreateCacheData<byte[]>(key, result.Message);
        data.Value = result.Message.Content != null && result.Message.Content.Length > 0
            ? result.Message.Content.ToArray()
            : Array.Empty<byte>();
        return data;
    }

    public Task<HorseCacheData<int>> GetIncrementalValue(string key, CancellationToken cancellationToken = default)
        => GetIncrementalValue(key, TimeSpan.Zero, 1, cancellationToken);

    public Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment, CancellationToken cancellationToken = default)
        => GetIncrementalValue(key, TimeSpan.Zero, increment, cancellationToken);

    public Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, CancellationToken cancellationToken = default)
        => GetIncrementalValue(key, duration, 1, cancellationToken);

    public async Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, int increment, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetIncrementalCache);

        if (increment != 1)
            message.AddHeader(HorseHeaders.VALUE, increment.ToString());

        if (duration > TimeSpan.Zero)
            message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());

        HorseResult result = await _client.SendAsync(message, true, cancellationToken);

        if (result == null || result.Code != HorseResultCode.Ok)
            return null;

        HorseCacheData<byte[]> data = CreateCacheData<byte[]>(key, result.Message);
        data.Value = result.Message.Content.ToArray();
        int value = BitConverter.ToInt32(data.Value);
        return new HorseCacheData<int>
        {
            Key = data.Key,
            Expiration = data.Expiration,
            WarnCount = data.WarnCount,
            WarningDate = data.WarningDate,
            IsFirstWarnedClient = data.IsFirstWarnedClient,
            Tags = data.Tags,
            Value = value
        };
    }

    private static HorseCacheData<T> CreateCacheData<T>(string key, HorseMessage message)
    {
        string expiry = message.FindHeader(HorseHeaders.EXPIRY);
        string warning = message.FindHeader(HorseHeaders.WARNING);
        string warnCount = message.FindHeader(HorseHeaders.WARN_COUNT);
        string tags = message.FindHeader(HorseHeaders.TAG);

        HorseCacheData<T> data = new HorseCacheData<T>
        {
            Key = key,
            IsFirstWarnedClient = message.HighPriority,
            Expiration = string.IsNullOrEmpty(expiry) ? 0 : Convert.ToInt64(expiry),
            WarningDate = string.IsNullOrEmpty(warning) ? 0 : Convert.ToInt64(warning),
            WarnCount = string.IsNullOrEmpty(warnCount) ? 0 : Convert.ToInt32(warnCount),
            Tags = string.IsNullOrEmpty(tags) ? Array.Empty<string>() : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        };

        return data;
    }

    public Task<HorseModelResult<List<CacheInformation>>> List(CancellationToken cancellationToken = default)
        => List(null, cancellationToken);

    public Task<HorseModelResult<List<CacheInformation>>> List(string filter, CancellationToken cancellationToken = default)
        => List(filter, 0, 0, cancellationToken);

    public Task<HorseModelResult<List<CacheInformation>>> List(int offset, int limit, CancellationToken cancellationToken = default)
        => List(null, offset, limit, cancellationToken);

    public Task<HorseModelResult<List<CacheInformation>>> List(string filter, int offset, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Cache;
        message.SetMessageId(_client.UniqueIdGenerator.Create());
        message.ContentType = KnownContentTypes.GetCacheList;

        // A lone '*' means no filter. Older servers cannot parse that pattern.
        if (!string.IsNullOrEmpty(filter) && filter != "*")
            message.AddHeader(HorseHeaders.FILTER, filter);

        if (offset > 0)
            message.AddHeader(HorseHeaders.CACHE_OFFSET, offset.ToString(CultureInfo.InvariantCulture));
        if (limit > 0)
            message.AddHeader(HorseHeaders.CACHE_LIMIT, limit.ToString(CultureInfo.InvariantCulture));

        Task<HorseModelResult<List<CacheInformation>>> response = _client.SendAsync<List<CacheInformation>>(message, cancellationToken);
        return offset == 0 && limit == 0 ? response : ReadPage(response, offset, limit);
    }

    private static async Task<HorseModelResult<List<CacheInformation>>> ReadPage(
        Task<HorseModelResult<List<CacheInformation>>> response, int offset, int limit)
    {
        HorseModelResult<List<CacheInformation>> result = await response.ConfigureAwait(false);
        if (result?.Result?.Code != HorseResultCode.Ok || result.Model == null)
            return result;

        HorseMessage message = result.Result.Message;
        if (message != null &&
            message.FindHeader(HorseHeaders.CACHE_OFFSET) == offset.ToString(CultureInfo.InvariantCulture) &&
            message.FindHeader(HorseHeaders.CACHE_LIMIT) == limit.ToString(CultureInfo.InvariantCulture))
            return result;

        // Old servers ignore unknown paging headers and return the complete
        // filtered list. Only trust server-side paging after an explicit echo.
        List<CacheInformation> ordered = new(result.Model);
        ordered.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key));
        int start = Math.Min(offset, ordered.Count);
        int count = limit == 0 ? ordered.Count - start : Math.Min(limit, ordered.Count - start);
        return new HorseModelResult<List<CacheInformation>>(result.Result, ordered.GetRange(start, count));
    }

    #endregion

    #region Set

    public Task<HorseResult> Set<TData>(string key, TData data, CancellationToken cancellationToken = default)
        => Set(key, data, null, false, cancellationToken);

    public Task<HorseResult> Set<TData>(string key, TData data, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        _client.MessageSerializer.Serialize(message, data);
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, CancellationToken cancellationToken = default)
        => Set(key, data, duration, null, false, cancellationToken);

    public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        _client.MessageSerializer.Serialize(message, data);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        _client.MessageSerializer.Serialize(message, data);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        message.SetOrAddHeader(HorseHeaders.WARNING_DURATION, Convert.ToInt32(expirationWarningDuration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetString(string key, string data, CancellationToken cancellationToken = default)
        => SetString(key, data, null, false, cancellationToken);

    public Task<HorseResult> SetString(string key, string data, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetStringContent(data);
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetString(string key, string data, TimeSpan duration, CancellationToken cancellationToken = default)
        => SetString(key, data, duration, null, false, cancellationToken);

    public Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetStringContent(data);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetStringContent(data);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        message.SetOrAddHeader(HorseHeaders.WARNING_DURATION, Convert.ToInt32(expirationWarningDuration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetData(string key, byte[] data, CancellationToken cancellationToken = default)
        => SetData(key, data, null, false, cancellationToken);

    public Task<HorseResult> SetData(string key, byte[] data, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        message.Content = new MemoryStream(data);
        message.CalculateLengths();
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, CancellationToken cancellationToken = default)
        => SetData(key, data, duration, null, false, cancellationToken);

    public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        message.Content = new MemoryStream(data);
        message.CalculateLengths();
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        message.SetOrAddHeader(HorseHeaders.WARNING_DURATION, Convert.ToInt32(expirationWarningDuration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, string.Join(',', tags));
        message.Content = new MemoryStream(data);
        message.CalculateLengths();
        return _client.SendAsync(message, true, cancellationToken);
    }

    #endregion

    #region Remove

    public Task<HorseResult> Remove(string key, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.RemoveCache);
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> Purge(CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, null, KnownContentTypes.PurgeCache);
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> PurgeByTag(string tag, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, null, KnownContentTypes.PurgeCache);
        message.SetOrAddHeader(HorseHeaders.TAG, tag);
        return _client.SendAsync(message, true, cancellationToken);
    }

    #endregion
}
