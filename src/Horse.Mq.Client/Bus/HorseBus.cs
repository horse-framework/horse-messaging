using System.Threading.Tasks;
using Horse.Mq.Client.Connectors;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Bus
{
    /// <summary>
    /// Implementation for sending messages to Horse MQ
    /// </summary>
    public class HorseBus : IHorseBus
    {
        private readonly HmqStickyConnector _connector;

        /// <summary>
        /// Gets connector of the bus
        /// </summary>
        public HmqStickyConnector Connector => _connector;

        /// <summary>
        /// Implementation for direct messages and requests
        /// </summary>
        public IHorseDirectBus Direct { get; protected set; }

        /// <summary>
        /// Implementation for queue messages and requests
        /// </summary>
        public IHorseQueueBus Queue { get; protected set; }

        /// <summary>
        /// Implementation for route messages and requests
        /// </summary>
        public IHorseRouteBus Route { get; protected set; }

        /// <summary>
        /// Creates new horse bus
        /// </summary>
        public HorseBus(HmqStickyConnector connector)
        {
            _connector = connector;

            Direct = new HorseDirectBus(connector);
            Queue = new HorseQueueBus(connector);
            Route = new HorseRouteBus(connector);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendAsync(HorseMessage message)
        {
            HorseClient client = GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.SendAsync(message);
        }

        /// <inheritdoc />
        public Task<HorseMessage> RequestAsync(HorseMessage message)
        {
            HorseClient client = GetClient();
            if (client == null)
                return Task.FromResult<HorseMessage>(null);

            return client.Request(message);
        }

        /// <inheritdoc />
        public HorseClient GetClient()
        {
            return _connector.GetClient();
        }
    }
}