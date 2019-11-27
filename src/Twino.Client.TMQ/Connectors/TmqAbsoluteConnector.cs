using System;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Absolute connector for TMQ protocol.
    /// </summary>
    public class TmqAbsoluteConnector : TmqStickyConnector
    {
        public TmqAbsoluteConnector(TimeSpan reconnectInterval) : base(reconnectInterval)
        {
        }
    }
}