using System;
using Twino.Client.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    public class TmqStickyConnector : StickyConnector<TmqClient, TmqMessage>
    {
        public TmqStickyConnector(TimeSpan reconnectInterval) : base(reconnectInterval)
        {
        }
    }
}