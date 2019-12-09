using System;
using Twino.Client.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Single message connector for TMQ protocol.
    /// </summary>
    public class TmqSingleMessageConnector : SingleMessageConnector<TmqClient, TmqMessage>
    {
        public TmqSingleMessageConnector(Func<TmqClient> createInstance = null) : base(createInstance)
        {
        }
    }
}