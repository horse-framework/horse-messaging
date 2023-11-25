using System;
using System.IO;

namespace Horse.Messaging.Server.Cache;

/// <summary>
/// Cache Item
/// </summary>
public class HorseCacheItem
{
    /// <summary>
    /// Cache key
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public DateTime Expiration { get; set; }

    /// <summary>
    /// The date for warning of key expiration.
    /// After that time, requester will receive extra information that tells the cache is about to expire but not expired yet. 
    /// </summary>
    public DateTime? ExpirationWarning { get; set; }

    /// <summary>
    /// How many times requesters are warned.
    /// </summary>
    public int ExpirationWarnCount { get; set; }

    /// <summary>
    /// Cache key tags.
    /// Cache keys can be removed by their tags.
    /// </summary>
    public string[] Tags { get; set; }

    /// <summary>
    /// Cache value
    /// </summary>
    public MemoryStream Value { get; set; }
}