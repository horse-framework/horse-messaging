using Twino.MQ.Client.Bus;
using Twino.MQ.Client.Connectors;

namespace Twino.MQ.Bus.Internal
{
    internal class TwinoBus<TIdentifier> : TwinoBus, ITwinoBus<TIdentifier>
    {
        internal TwinoBus(TmqStickyConnector connector) : base(connector)
        {
            Direct = new TwinoDirectBus<TIdentifier>(connector);
            Queue = new TwinoQueueBus<TIdentifier>(connector);
            Route = new TwinoRouteBus<TIdentifier>(connector);
        }
    }
}