using System;
using Twino.Client.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Sticky connector for TMQ protocol.
    /// </summary>
    public class TmqStickyConnector : StickyConnector<TmqClient, TmqMessage>
    {
        public TmqStickyConnector(TimeSpan reconnectInterval, Func<TmqClient> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
        }
    }
}