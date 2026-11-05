using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;

namespace Horse.Messaging.Server.Cache;

internal class CacheNetworkHandler : INetworkMessageHandler
{
    private readonly HorseRider _rider;
    private readonly HorseCache _cache;

    public CacheNetworkHandler(HorseRider rider)
    {
        _rider = rider;
        _cache = rider.Cache;
    }

    public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
    {
        try
        {
            await HandleUnsafe(client, message);
        }
        catch (OperationCanceledException)
        {
            await client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));
        }
        catch
        {
            await client.SendAsync(message.CreateResponse(HorseResultCode.Failed));
        }
    }

    private async Task HandleUnsafe(MessagingClient client, HorseMessage message)
    {
        switch (message.ContentType)
        {
            //get cache item
            case KnownContentTypes.GetCache:
            {
                foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                {
                    if (!authorization.CanGet(client, message.Target))
                    {
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                        return;
                    }
                }

                if (!_cache.TryGet(message.Target, out var result))
                {
                    await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
                    return;
                }

                HorseCacheItem item = result.Item;
                HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                response.SetSource(message.Target);

                AddItemHeaders(response, item);

                client.Stats.CacheGets++;
                response.HighPriority = result.IsFirstWarningReceiver;
                response.Content = item.Value;

                await client.SendAsync(response);
                _cache.GetEvent.Trigger(client, message.Target);
                return;
            }

            //set cache item
            case KnownContentTypes.SetCache:
            {
                foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                {
                    if (!authorization.CanSet(client, message.Target, message.Content))
                    {
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                        return;
                    }
                }

                string messageTimeout = message.FindHeader(HorseHeaders.MESSAGE_TIMEOUT);
                string warningDuration = message.FindHeader(HorseHeaders.WARNING_DURATION);
                string persistentCache = message.FindHeader(HorseHeaders.PERSISTENT_CACHE);
                string tags = message.FindHeader(HorseHeaders.TAG);

                string[] tagNames = string.IsNullOrEmpty(tags) ? [] : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                TimeSpan timeout = TimeSpan.Zero;
                TimeSpan? warning = null;
                bool persistent = false;

                if (!string.IsNullOrEmpty(messageTimeout))
                    timeout = TimeSpan.FromSeconds(Convert.ToInt32(messageTimeout));

                if (!string.IsNullOrEmpty(warningDuration))
                    warning = TimeSpan.FromSeconds(Convert.ToInt32(warningDuration));

                if (!string.IsNullOrEmpty(persistentCache))
                    persistent = string.Equals(persistentCache, "1") || string.Equals(persistentCache, "true", StringComparison.OrdinalIgnoreCase);

                CacheOperation operation = await _cache.SetAsync(client, !client.IsNodeClient, message.Target, message.Content, timeout, warning, tagNames, persistent);
                switch (operation.Result)
                {
                    case CacheResult.Ok:
                        client.Stats.CacheSets++;
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Ok));
                        return;

                    case CacheResult.KeyLimit:
                        await client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));
                        return;

                    case CacheResult.KeySizeLimit:
                        await client.SendAsync(message.CreateResponse(HorseResultCode.NameSizeLimit));
                        return;

                    case CacheResult.ItemSizeLimit:
                        await client.SendAsync(message.CreateResponse(HorseResultCode.ValueSizeLimit));
                        return;

                    default:
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Failed));
                        return;
                }
            }

            case KnownContentTypes.GetIncrementalCache:
            {
                foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                {
                    if (!authorization.CanSet(client, message.Target, message.Content))
                    {
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                        return;
                    }
                }

                string messageTimeout = message.FindHeader(HorseHeaders.MESSAGE_TIMEOUT);
                string incrementalValue = message.FindHeader(HorseHeaders.VALUE);
                int increment = string.IsNullOrEmpty(incrementalValue) ? 1 : Convert.ToInt32(incrementalValue);
                string tags = message.FindHeader(HorseHeaders.TAG);
                string[] tagNames = string.IsNullOrEmpty(tags) ? Array.Empty<string>() : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                TimeSpan timeout = TimeSpan.Zero;
                if (!string.IsNullOrEmpty(messageTimeout))
                    timeout = TimeSpan.FromSeconds(Convert.ToInt32(messageTimeout));

                GetCacheItemResult getResult = _cache.GetIncremental(message.Target, timeout, increment, tagNames);
                if (getResult?.Item == null)
                {
                    await client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));
                    return;
                }
                HorseCacheItem item = getResult.Item;

                HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                response.SetSource(message.Target);

                AddItemHeaders(response, item);

                client.Stats.CacheGets++;
                response.HighPriority = getResult.IsFirstWarningReceiver;
                response.Content = item.Value;

                await client.SendAsync(response);
                _cache.GetEvent.Trigger(client, message.Target);
                break;
            }

            //remove cache item
            case KnownContentTypes.RemoveCache:
            {
                foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                {
                    if (!authorization.CanRemove(client, message.Target))
                    {
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                        return;
                    }
                }

                _cache.Remove(client, message.Target, true);
                await client.SendAsync(message.CreateResponse(HorseResultCode.Ok));
                return;
            }

            //purge all caches
            case KnownContentTypes.PurgeCache:
            {
                foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                {
                    if (!authorization.CanPurge(client))
                    {
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                        return;
                    }
                }

                string tagName = message.FindHeader(HorseHeaders.TAG);

                if (string.IsNullOrEmpty(tagName))
                    _cache.Purge(client, true);
                else
                    _cache.PurgeByTag(tagName.Trim(), client, true);

                await client.SendAsync(message.CreateResponse(HorseResultCode.Ok));
                return;
            }

            case KnownContentTypes.GetCacheList:
            {
                string filter = message.FindHeader(HorseHeaders.FILTER);
                int offset = ReadPageValue(message, HorseHeaders.CACHE_OFFSET);
                int limit = ReadPageValue(message, HorseHeaders.CACHE_LIMIT);
                ICacheAuthorization[] authorizations = _cache.Authorizations.All();
                List<CacheInformation> caches = authorizations.Length == 0
                    ? _cache.GetCacheKeys(filter, offset, limit)
                    : _cache.GetCacheKeys(filter, offset, limit, key =>
                    {
                        foreach (ICacheAuthorization authorization in authorizations)
                            if (!authorization.CanGet(client, key))
                                return false;
                        return true;
                    });

                HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                response.ContentType = KnownContentTypes.ChannelList;
                if (offset > 0 || limit > 0)
                {
                    response.AddHeader(HorseHeaders.CACHE_OFFSET, offset.ToString(CultureInfo.InvariantCulture));
                    response.AddHeader(HorseHeaders.CACHE_LIMIT, limit.ToString(CultureInfo.InvariantCulture));
                }
                response.Serialize(caches, _rider.MessageContentSerializer);
                await client.SendAsync(response);
                return;
            }
        }
    }

    private static void AddItemHeaders(HorseMessage response, HorseCacheItem item)
    {
        if (item.TagHeader != null)
            response.AddHeader(HorseHeaders.TAG, item.TagHeader);

        response.AddHeader(HorseHeaders.EXPIRY, item.ExpirationHeader);

        if (item.WarningHeader != null)
        {
            response.AddHeader(HorseHeaders.WARNING, item.WarningHeader);
            if (item.ExpirationWarnCount > 0)
                response.AddHeader(HorseHeaders.WARN_COUNT, item.ExpirationWarnCount.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static int ReadPageValue(HorseMessage message, string header)
    {
        string value = message.FindHeader(header);
        if (string.IsNullOrEmpty(value))
            return 0;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result))
            throw new ArgumentException("Cache paging values must be non-negative integers.", header);
        return result;
    }
}
