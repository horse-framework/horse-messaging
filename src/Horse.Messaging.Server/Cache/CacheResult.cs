namespace Horse.Messaging.Server.Cache;

/// <summary>
/// Cache operation result
/// </summary>
public enum CacheResult
{
    /// <summary>
    /// Operation is successful
    /// </summary>
    Ok,

    /// <summary>
    /// Operation failed because maximum key limit exceeded
    /// </summary>
    KeyLimit,

    /// <summary>
    /// Operation failed because key size is too large
    /// </summary>
    KeySizeLimit,

    /// <summary>
    /// Operation failed because item size is too large
    /// </summary>
    ItemSizeLimit
}