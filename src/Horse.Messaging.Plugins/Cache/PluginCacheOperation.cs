namespace Horse.Messaging.Plugins.Cache;

/// <summary>
/// The result object of cache operations
/// </summary>
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