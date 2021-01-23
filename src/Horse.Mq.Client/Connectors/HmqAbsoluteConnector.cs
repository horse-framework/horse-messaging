using System;

namespace Horse.Mq.Client.Connectors
{
    /// <summary>
    /// Absolute connector for HMQ protocol.
    /// </summary>
    public class HmqAbsoluteConnector : HmqStickyConnector
    {
        /// <summary>
        /// Creates new absolute connector for HMQ protocol clients
        /// </summary>
        public HmqAbsoluteConnector(TimeSpan reconnectInterval, Func<HorseClient> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
        }
    }
}