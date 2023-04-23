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
        
        /// <summary>
        /// Key expiration warning date in unix milliseconds
        /// </summary>
        public long WarningDate { get; set; }

        /// <summary>
        /// Count value of how many times expiration warned
        /// </summary>
        public int WarnCount { get; set; }
    }
}