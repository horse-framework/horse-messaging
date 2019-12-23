namespace Twino.MQ.Options
{
    /// <summary>
    /// Server default options
    /// </summary>
    public class MqServerOptions : ChannelOptions
    {
        /// <summary>
        /// Server name, will be used while connecting to other instances
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Server type, will be used while connecting to other instances
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Other server instance informations that will be connected
        /// </summary>
        public InstanceOptions[] Instances { get; set; }
    }
}