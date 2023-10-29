using System;
using System.Linq;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;

namespace Horse.Messaging.Server.Cache;

internal class CacheClusterNotifier
{
    private readonly HorseCache _cache;
    private readonly ClusterManager _cluster;

    public CacheClusterNotifier(HorseCache cache, ClusterManager cluster)
    {
        _cache = cache;
        _cluster = cluster;
    }

    internal void SendSet(HorseCacheItem cache)
    {
        HorseMessage message = new HorseMessage(MessageType.Cluster, cache.Key, KnownContentTypes.SetCache);

        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32((cache.Expiration - DateTime.UtcNow).TotalSeconds).ToString());

        if (cache.ExpirationWarning.HasValue)
            message.SetOrAddHeader(HorseHeaders.WARNING_DURATION, Convert.ToInt32((cache.ExpirationWarning.Value - DateTime.UtcNow).TotalSeconds).ToString());

        if (cache.Tags != null && cache.Tags.Length > 0)
            message.SetOrAddHeader(HorseHeaders.TAG, cache.Tags.Aggregate((t, i) => $"{t},{i}"));

        _cluster.SendMessage(message);
    }

    internal void SendRemove(string key)
    {
        HorseMessage msg = new HorseMessage(MessageType.Cluster, key, KnownContentTypes.RemoveCache);
        _cluster.SendMessage(msg);
    }

    internal void SendPurge(string tagName)
    {
        HorseMessage msg = new HorseMessage(MessageType.Cluster, tagName, KnownContentTypes.PurgeCache);
        _cluster.SendMessage(msg);
    }
}