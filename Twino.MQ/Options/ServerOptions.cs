namespace Twino.MQ.Options
{
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
    }
}