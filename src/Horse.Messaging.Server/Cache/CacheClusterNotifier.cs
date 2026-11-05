using System;
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
        if (!HasConnectedNodes())
            return;

        HorseMessage message = new HorseMessage(MessageType.Cluster, cache.Key, KnownContentTypes.SetCache)
        {
            Content = cache.Value
        };

        message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32((cache.Expiration - DateTime.UtcNow).TotalSeconds).ToString());

        if (cache.ExpirationWarning.HasValue)
            message.SetOrAddHeader(HorseHeaders.WARNING_DURATION, Convert.ToInt32((cache.ExpirationWarning.Value - DateTime.UtcNow).TotalSeconds).ToString());

        if (cache.TagHeader != null)
            message.SetOrAddHeader(HorseHeaders.TAG, cache.TagHeader);

        if (cache.IsPersistent)
            message.SetOrAddHeader(HorseHeaders.PERSISTENT_CACHE, "true");

        _cluster.SendMessage(message);
    }

    internal void SendRemove(string key)
    {
        if (!HasConnectedNodes())
            return;
        HorseMessage msg = new HorseMessage(MessageType.Cluster, key, KnownContentTypes.RemoveCache);
        _cluster.SendMessage(msg);
    }

    internal void SendPurge(string tagName)
    {
        if (!HasConnectedNodes())
            return;
        HorseMessage msg = new HorseMessage(MessageType.Cluster, tagName, KnownContentTypes.PurgeCache);
        _cluster.SendMessage(msg);
    }

    private bool HasConnectedNodes()
    {
        foreach (NodeClient client in _cluster.Clients)
            if (client != null && client.IsConnected)
                return true;
        return false;
    }
}
