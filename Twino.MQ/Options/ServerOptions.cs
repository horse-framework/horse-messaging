namespace Twino.MQ.Options
{
    /// <summary>
    /// Server default options
    /// </summary>
    public class ServerOptions : ChannelOptions
    {
        /// <summary>
        /// Server default TTL value
        /// </summary>
        public int Ttl { get; set; }

        /// <summary>
        /// Server port listening options
        /// </summary>
        public ListenerOptions[] Listeners { get; set; }
        
        /// <summary>
        /// Maximum client count
        /// </summary>
        public int ClientCapacity { get; set; }
        
        /// <summary>
        /// Maximum channel count
        /// </summary>
        public int ChannelCapacity { get; set; }
    }
}