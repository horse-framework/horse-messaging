using Horse.Mq.Client.Bus;
using Horse.Mq.Client.Connectors;

namespace Horse.Mq.Bus.Internal
{
    internal class HorseDirectBus<TIdentifier> : HorseDirectBus, IHorseDirectBus<TIdentifier>
    {
        internal HorseDirectBus(HmqStickyConnector connector) : base(connector)
        {
        }
    }
}