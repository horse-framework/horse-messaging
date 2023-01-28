namespace Horse.Messaging.Protocol.Models
{
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
        /// Key expiration in unix milliseconds
        /// </summary>
        public long Expiration { get; set; }
    }
}