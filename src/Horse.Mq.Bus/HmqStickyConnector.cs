using System;
using Horse.Mq.Bus.Internal;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;

namespace Horse.Mq.Bus
{
    /// <summary>
    /// Used for using multiple Horse Bus in same provider.
    /// Template type is the identifier
    /// </summary>
    public class HmqStickyConnector<TIdentifier> : HmqStickyConnector
    {
        internal HmqStickyConnector(TimeSpan reconnectInterval, Func<HorseClient> createInstance = null) : base(reconnectInterval, createInstance)
        {
            Bus = new HorseBus<TIdentifier>(this);
        }
    }
}