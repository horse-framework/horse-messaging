using System.Threading.Tasks;
using Twino.Client.TMQ.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Bus
{
    /// <summary>
    /// Implementation for sending messages to Twino MQ
    /// </summary>
    public class TwinoBus : ITwinoBus
    {
        private readonly TmqStickyConnector _connector;

        /// <summary>
        /// Implementation for direct messages and requests
        /// </summary>
        public ITwinoDirectBus Direct { get; }

        /// <summary>
        /// Implementation for queue messages and requests
        /// </summary>
        public ITwinoQueueBus Queue { get; }

        /// <summary>
        /// Implementation for route messages and requests
        /// </summary>
        public ITwinoRouteBus Route { get; }

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
        public Task<TwinoResult> SendAsync(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.SendAsync(message);
        }

        /// <inheritdoc />
        public Task<TmqMessage> RequestAsync(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client == null)
                return Task.FromResult<TmqMessage>(null);

            return client.Request(message);
        }

        /// <inheritdoc />
        public TmqClient GetClient()
        {
            return _connector.GetClient();
        }
    }
}