using System;

namespace Twino.Client.TMQ.Connectors
{
    public class TmqAbsoluteConnector : TmqStickyConnector
    {
        public TmqAbsoluteConnector(TimeSpan reconnectInterval) : base(reconnectInterval)
        {
        }
    }
}