namespace Horse.Messaging.Client.Cache;

/// <summary>
/// Horse Cache Data
/// </summary>
/// <typeparam name="T"></typeparam>
public class HorseCacheData<T>
{
    /// <summary>
    /// Cache data value
    /// </summary>
    public T Value { get; internal set; }

    /// <summary>
    /// True, if first expiration warning message sent to the client.
    /// If data should be refreshed before expiration, that client is responsible to do that.
    /// </summary>
    public bool IsFirstWarnedClient { get; internal set; }

    /// <summary>
    /// Cache key
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Cache expiration data in unix milliseconds
    /// </summary>
    public long Expiration { get; internal set; }

    /// <summary>
    /// If exists, cache warning date in unix milliseconds
    /// </summary>
    public long? WarningDate { get; internal set; }

    /// <summary>
    /// If cache expiration warned more than one, this is the warning value
    /// </summary>
    public int WarnCount { get; internal set; }

    /// <summary>
    /// Key Tags 
    /// </summary>
    public string[] Tags { get; set; }
}