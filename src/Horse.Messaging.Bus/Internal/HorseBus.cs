using Horse.Messaging.Client.Bus;
using Horse.Mq.Client.Connectors;

namespace Horse.Messaging.Bus.Internal
{
    internal class HorseBus<TIdentifier> : HorseBus, IHorseBus<TIdentifier>
    {
        internal HorseBus(HmqStickyConnector connector) : base(connector)
        {
            Direct = new HorseDirectBus<TIdentifier>(connector);
            Queue = new HorseQueueBus<TIdentifier>(connector);
            Route = new HorseRouteBus<TIdentifier>(connector);
        }
    }
}