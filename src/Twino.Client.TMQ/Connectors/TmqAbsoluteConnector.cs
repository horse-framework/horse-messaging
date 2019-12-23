using System;
using System.Security.Cryptography;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Absolute connector for TMQ protocol.
    /// </summary>
    public class TmqAbsoluteConnector : TmqStickyConnector
    {
        public TmqAbsoluteConnector(TimeSpan reconnectInterval, Func<TmqClient> createInstance = null) 
            : base(reconnectInterval, createInstance)
        {
        }
    }
}