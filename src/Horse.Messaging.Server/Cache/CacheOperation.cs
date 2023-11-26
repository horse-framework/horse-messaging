namespace Horse.Messaging.Server.Cache;

/// <summary>
/// Result object for cache operation
/// </summary>
public class CacheOperation
{
    /// <summary>
    /// Cached item
    /// </summary>
    public HorseCacheItem Item { get; }

    /// <summary>
    /// Result code
    /// </summary>
    public CacheResult Result { get; }

    /// <summary>
    /// Creates new cache operation
    /// </summary>
    public CacheOperation(CacheResult result, HorseCacheItem item)
    {
        Result = result;
        Item = item;
    }
}