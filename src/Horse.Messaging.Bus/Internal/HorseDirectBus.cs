using Horse.Messaging.Client.Bus;
using Horse.Messaging.Client.Direct;
using Horse.Mq.Client.Connectors;

namespace Horse.Messaging.Bus.Internal
{
    internal class HorseDirectBus<TIdentifier> : HorseDirectBus, IHorseDirectBus<TIdentifier>
    {
        internal HorseDirectBus(HmqStickyConnector connector) : base(connector)
        {
        }
    }
}