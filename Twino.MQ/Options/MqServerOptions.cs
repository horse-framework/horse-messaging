namespace Twino.MQ.Options
{
    /// <summary>
    /// Server default options
    /// </summary>
    public class MqServerOptions : ChannelOptions
    {
        /// <summary>
        /// Server port listening options
        /// </summary>
        public ListenerOptions[] Listeners { get; set; }
    }
}