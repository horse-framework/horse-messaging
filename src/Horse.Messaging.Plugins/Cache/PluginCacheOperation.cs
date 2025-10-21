namespace Horse.Messaging.Plugins.Cache;

public class PluginCacheOperation
{
    /// <summary>
    /// Cached item
    /// </summary>
    public PluginCacheItem Item { get; }

    /// <summary>
    /// Result code
    /// </summary>
    public PluginCacheResult Result { get; }

    /// <summary>
    /// Creates new cache operation
    /// </summary>
    public PluginCacheOperation(PluginCacheResult result, PluginCacheItem item)
    {
        Result = result;
        Item = item;
    }
}