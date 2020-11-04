using System.Threading.Tasks;
using Twino.MQ.Client.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client.Bus
{
    /// <summary>
    /// Implementation for sending messages to Twino MQ
    /// </summary>
    public class TwinoBus : ITwinoBus
    {
        private readonly TmqStickyConnector _connector;

        /// <summary>
        /// Gets connector of the bus
        /// </summary>
        public TmqStickyConnector Connector => _connector;

        /// <summary>
        /// Implementation for direct messages and requests
        /// </summary>
        public ITwinoDirectBus Direct { get; protected set; }

        /// <summary>
        /// Implementation for queue messages and requests
        /// </summary>
        public ITwinoQueueBus Queue { get; protected set; }

        /// <summary>
        /// Implementation for route messages and requests
        /// </summary>
        public ITwinoRouteBus Route { get; protected set; }

        /// <summary>
        /// Creates new twino bus
        /// </summary>
        public TwinoBus(TmqStickyConnector connector)
        {
            _connector = connector;

            Direct = new TwinoDirectBus(connector);
            Queue = new TwinoQueueBus(connector);
            Route = new TwinoRouteBus(connector);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendAsync(TwinoMessage message)
        {
            TmqClient client = GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.SendAsync(message);
        }

        /// <inheritdoc />
        public Task<TwinoMessage> RequestAsync(TwinoMessage message)
        {
            TmqClient client = GetClient();
            if (client == null)
                return Task.FromResult<TwinoMessage>(null);

            return client.Request(message);
        }

        /// <inheritdoc />
        public TmqClient GetClient()
        {
            return _connector.GetClient();
        }
    }
}