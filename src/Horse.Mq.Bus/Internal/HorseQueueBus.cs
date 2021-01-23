using Horse.Mq.Client.Bus;
using Horse.Mq.Client.Connectors;

namespace Horse.Mq.Bus.Internal
{
    internal class HorseQueueBus<TIdentifier> : HorseQueueBus, IHorseQueueBus<TIdentifier>
    {
        internal HorseQueueBus(HmqStickyConnector connector) : base(connector)
        {
        }
    }
}