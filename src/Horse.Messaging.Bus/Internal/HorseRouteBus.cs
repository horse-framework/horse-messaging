using Horse.Messaging.Client.Bus;
using Horse.Messaging.Client.Routers;
using Horse.Mq.Client.Connectors;

namespace Horse.Messaging.Bus.Internal
{
    internal class HorseRouteBus<TIdentifier> : HorseRouteBus, IHorseRouteBus<TIdentifier>
    {
        internal HorseRouteBus(HmqStickyConnector connector) : base(connector)
        {
        }
    }
}