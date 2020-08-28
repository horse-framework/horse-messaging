using Twino.Server;

namespace Twino.MQ.Options
{
    /// <summary>
    /// Server default options
    /// </summary>
    public class TwinoMqOptions : QueueOptions
    {
        /// <summary>
        /// Server name, will be used while connecting to other instances
        /// </summary>
        public string Name { get; set; } = "twino-mq";

        /// <summary>
        /// Server type, will be used while connecting to other instances
        /// </summary>
        public string Type { get; set; } = "mq";

        /// <summary>
        /// If true, queue will be created automatically with default options
        /// when a client tries to subscribe or push a message to not existing queue.
        /// </summary>
        public bool AutoQueueCreation { get; set; } = true;

        /// <summary>
        /// Maximum queue limit of the server
        /// Zero is unlimited.
        /// </summary>
        public int QueueLimit { get; set; }

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