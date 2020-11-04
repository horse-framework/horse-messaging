using Twino.MQ.Client.Bus;
using Twino.MQ.Client.Connectors;

namespace Twino.MQ.Bus.Internal
{
    internal class TwinoRouteBus<TIdentifier> : TwinoRouteBus, ITwinoRouteBus<TIdentifier>
    {
        internal TwinoRouteBus(TmqStickyConnector connector) : base(connector)
        {
        }
    }
}