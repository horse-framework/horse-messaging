using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public async Task<HorseCacheData<TData>> Get<TData>(string key, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
        HorseModelResult<TData> result = await _client.SendAsync<TData>(message, cancellationToken);

        if (result == null || result.Result?.Code != HorseResultCode.Ok || result.Result.Message == null)
            return default;

        HorseCacheData<TData> data = CreateCacheData<TData>(key, result.Result.Message);
        data.Value = result.Model;
        return data;
    }

    public async Task<HorseCacheData<string>> GetString(string key, CancellationToken cancellationToken)
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

    public async Task<HorseCacheData<byte[]>> GetData(string key, CancellationToken cancellationToken)
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

    public Task<HorseCacheData<int>> GetIncrementalValue(string key, CancellationToken cancellationToken)
        => GetIncrementalValue(key, TimeSpan.Zero, 1, cancellationToken);

    public Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment, CancellationToken cancellationToken)
        => GetIncrementalValue(key, TimeSpan.Zero, increment, cancellationToken);

    public Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, CancellationToken cancellationToken)
        => GetIncrementalValue(key, duration, 1, cancellationToken);

    public async Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, int increment, CancellationToken cancellationToken)
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

    public Task<HorseModelResult<List<CacheInformation>>> List(CancellationToken cancellationToken)
        => List(null, cancellationToken);

    public async Task<HorseModelResult<List<CacheInformation>>> List(string filter, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Cache;
        message.SetMessageId(_client.UniqueIdGenerator.Create());
        message.ContentType = KnownContentTypes.GetCacheList;

        if (!string.IsNullOrEmpty(filter))
            message.AddHeader(HorseHeaders.FILTER, filter);

        return await _client.SendAsync<List<CacheInformation>>(message, cancellationToken);
    }

    #endregion

    #region Set

    public Task<HorseResult> Set<TData>(string key, TData data, CancellationToken cancellationToken)
        => Set(key, data, null, false, cancellationToken);

    public Task<HorseResult> Set<TData>(string key, TData data, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        _client.MessageSerializer.Serialize(message, data);
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, CancellationToken cancellationToken)
        => Set(key, data, duration, null, false, cancellationToken);

    public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        _client.MessageSerializer.Serialize(message, data);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        _client.MessageSerializer.Serialize(message, data);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        message.SetOrAddHeader(HorseHeaders.WARNING_DURATION, Convert.ToInt32(expirationWarningDuration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetString(string key, string data, CancellationToken cancellationToken)
        => SetString(key, data, null, false, cancellationToken);

    public Task<HorseResult> SetString(string key, string data, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetStringContent(data);
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetString(string key, string data, TimeSpan duration, CancellationToken cancellationToken)
        => SetString(key, data, duration, null, false, cancellationToken);

    public Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetStringContent(data);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetStringContent(data);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        message.SetOrAddHeader(HorseHeaders.WARNING_DURATION, Convert.ToInt32(expirationWarningDuration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetData(string key, byte[] data, CancellationToken cancellationToken)
        => SetData(key, data, null, false, cancellationToken);

    public Task<HorseResult> SetData(string key, byte[] data, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        message.Content = new MemoryStream(data);
        message.CalculateLengths();
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, CancellationToken cancellationToken)
        => SetData(key, data, duration, null, false, cancellationToken);

    public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        message.Content = new MemoryStream(data);
        message.CalculateLengths();
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
        message.SetOrAddHeader(HorseHeaders.WARNING_DURATION, Convert.ToInt32(expirationWarningDuration.TotalSeconds).ToString());
        if (persistent) message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");
        if (tags != null && tags.Length > 0) message.SetOrAddHeader(HorseHeaders.TAG, tags.Aggregate((t, i) => $"{t},{i}"));
        message.Content = new MemoryStream(data);
        message.CalculateLengths();
        return _client.SendAsync(message, true, cancellationToken);
    }

    #endregion

    #region Remove

    public Task<HorseResult> Remove(string key, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.RemoveCache);
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> Purge(CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, null, KnownContentTypes.PurgeCache);
        return _client.SendAsync(message, true, cancellationToken);
    }

    public Task<HorseResult> PurgeByTag(string tag, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Cache, null, KnownContentTypes.PurgeCache);
        message.SetOrAddHeader(HorseHeaders.TAG, tag);
        return _client.SendAsync(message, true, cancellationToken);
    }

    #endregion
}