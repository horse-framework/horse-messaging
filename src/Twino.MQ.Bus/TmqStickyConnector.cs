using System;
using Twino.MQ.Bus.Internal;
using Twino.MQ.Client;
using Twino.MQ.Client.Connectors;

namespace Twino.MQ.Bus
{
    /// <summary>
    /// Used for using multiple twino bus in same provider.
    /// Template type is the identifier
    /// </summary>
    public class TmqStickyConnector<TIdentifier> : TmqStickyConnector
    {
        internal TmqStickyConnector(TimeSpan reconnectInterval, Func<TmqClient> createInstance = null) : base(reconnectInterval, createInstance)
        {
            Bus = new TwinoBus<TIdentifier>(this);
        }
    }
}