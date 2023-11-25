namespace Horse.Messaging.Protocol.Models;

/// <summary>
/// Cache key information
/// </summary>
public class CacheInformation
{
    /// <summary>
    /// Cache key
    /// </summary>
    public string Key { get; set; }
        
    /// <summary>
    /// Key expiration in unix seconds
    /// </summary>
    public long Expiration { get; set; }
        
    /// <summary>
    /// Key expiration warning date in unix seconds
    /// </summary>
    public long WarningDate { get; set; }

    /// <summary>
    /// Count value of how many times expiration warned
    /// </summary>
    public int WarnCount { get; set; }

    /// <summary>
    /// Tags
    /// </summary>
    public string[] Tags { get; set; }
}