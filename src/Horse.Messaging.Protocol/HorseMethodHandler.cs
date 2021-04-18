using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;

namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// HMQ Message received handler
    /// </summary>
    public delegate Task HorseMessageHandler(HorseServerSocket socket, HorseMessage message);

    /// <summary>
    /// HMQ message handler for action-based use
    /// </summary>
    public class HorseMethodHandler : IProtocolConnectionHandler<HorseServerSocket, HorseMessage>
    {
        /// <summary>
        /// Default unique Id generator
        /// </summary>
        private readonly IUniqueIdGenerator _uniqueIdGenerator = new DefaultUniqueIdGenerator();

        /// <summary>
        /// User defined message received handler
        /// </summary>
        private readonly HorseMessageHandler _action;

        /// <summary>
        /// Creates new HMQ protocol handler for action-based message handling use
        /// </summary>
        /// <param name="action"></param>
        public HorseMethodHandler(HorseMessageHandler action)
        {
            _action = action;
        }

        /// <summary>
        /// Triggered when a HMQ client is connected. 
        /// </summary>
        public async Task<HorseServerSocket> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data)
        {
            return await Task.FromResult(new HorseServerSocket(server, connection, _uniqueIdGenerator));
        }

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        public async Task Ready(IHorseServer server, HorseServerSocket client)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Triggered when a HMQ message received from client
        /// </summary>
        public async Task Received(IHorseServer server, IConnectionInfo info, HorseServerSocket client, HorseMessage message)
        {
            await _action(client, message);
        }

        /// <summary>
        /// Triggered when a HMQ client is connected. 
        /// </summary>
        public async Task Disconnected(IHorseServer server, HorseServerSocket client)
        {
            await Task.CompletedTask;
        }
    }
}