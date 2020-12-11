using Horse.Mq.Client.Bus;
using Horse.Mq.Client.Connectors;

namespace Horse.Mq.Bus.Internal
{
    internal class HorseRouteBus<TIdentifier> : HorseRouteBus, IHorseRouteBus<TIdentifier>
    {
        internal HorseRouteBus(HmqStickyConnector connector) : base(connector)
        {
        }
    }
}