using Horse.Mq.Client.Connectors;
using Horse.Mq.Options;

namespace Horse.Mq
{
    /// <summary>
    /// Node description for outgoing messages
    /// </summary>
    public class OutgoingNode
    {
        /// <summary>
        /// Node options
        /// </summary>
        public NodeOptions Options { get; set; }

        /// <summary>
        /// Node connector
        /// </summary>
        public HmqStickyConnector Connector { get; set; }
    }
}