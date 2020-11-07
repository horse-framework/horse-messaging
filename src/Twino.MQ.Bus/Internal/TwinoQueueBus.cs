using Twino.MQ.Client.Bus;
using Twino.MQ.Client.Connectors;

namespace Twino.MQ.Bus.Internal
{
    internal class TwinoQueueBus<TIdentifier> : TwinoQueueBus, ITwinoQueueBus<TIdentifier>
    {
        internal TwinoQueueBus(TmqStickyConnector connector) : base(connector)
        {
        }
    }
}