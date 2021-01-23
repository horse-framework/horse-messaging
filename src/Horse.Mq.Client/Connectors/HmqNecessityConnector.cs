using System;
using Horse.Client.Connectors;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Connectors
{
    /// <summary>
    /// Necessity connector for HMQ protocol.
    /// </summary>
    public class HmqNecessityConnector : NecessityConnector<HorseClient, HorseMessage>
    {
        /// <summary>
        /// Creates new necessity connector for HMQ protocol clients
        /// </summary>
        public HmqNecessityConnector(Func<HorseClient> createInstance = null) : base(createInstance)
        {
        }
    }
}