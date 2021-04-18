using Horse.Messaging.Client.Bus;
using Horse.Messaging.Client.Queues;
using Horse.Mq.Client.Connectors;

namespace Horse.Messaging.Bus.Internal
{
    internal class HorseQueueBus<TIdentifier> : HorseQueueBus, IHorseQueueBus<TIdentifier>
    {
        internal HorseQueueBus(HmqStickyConnector connector) : base(connector)
        {
        }
    }
}