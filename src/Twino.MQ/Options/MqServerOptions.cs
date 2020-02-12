using Twino.Server;

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
        /// If true, channel will be created automatically with default options
        /// when a client tries to subscribe or push a message to not existing channel.
        /// </summary>
        public bool AutoChannelCreation { get; set; }

        /// <summary>
        /// If true, queue will be created automatically with default options
        /// when a client tries to subscribe or push a message to not existing queue.
        /// </summary>
        public bool AutoQueueCreation { get; set; }

        /// <summary>
        /// Maximum channel limit of the server
        /// Zero is unlimited.
        /// </summary>
        public int ChannelLimit { get; set; }

        /// <summary>
        /// Other server node informations that will be connected
        /// </summary>
        public NodeOptions[] Nodes { get; set; }

        /// <summary>
        /// Node server host options
        /// </summary>
        public HostOptions NodeHost { get; set; }
    }
}