using Horse.Messaging.Client;
using Horse.Messaging.Server.Options;

namespace Horse.Messaging.Server
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
        /// Node client
        /// </summary>
        public HorseClient Client { get; set; }
    }
}