using Horse.Mq.Client.Bus;
using Horse.Mq.Client.Connectors;

namespace Horse.Mq.Bus.Internal
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