using Twino.MQ.Client.Bus;
using Twino.MQ.Client.Connectors;

namespace Twino.MQ.Bus.Internal
{
    internal class TwinoDirectBus<TIdentifier> : TwinoDirectBus, ITwinoDirectBus<TIdentifier>
    {
        internal TwinoDirectBus(TmqStickyConnector connector) : base(connector)
        {
        }
    }
}