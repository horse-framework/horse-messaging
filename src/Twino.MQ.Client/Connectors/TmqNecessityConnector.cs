using System;
using Twino.Client.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client.Connectors
{
    /// <summary>
    /// Necessity connector for TMQ protocol.
    /// </summary>
    public class TmqNecessityConnector : NecessityConnector<TmqClient, TwinoMessage>
    {
        /// <summary>
        /// Creates new necessity connector for TMQ protocol clients
        /// </summary>
        public TmqNecessityConnector(Func<TmqClient> createInstance = null) : base(createInstance)
        {
        }
    }
}