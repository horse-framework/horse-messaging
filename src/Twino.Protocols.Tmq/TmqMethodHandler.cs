using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Message received handler
    /// </summary>
    public delegate Task TmqMessageHandler(TmqServerSocket socket, TmqMessage message);

    /// <summary>
    /// TMQ message handler for action-based use
    /// </summary>
    public class TmqMethodHandler : IProtocolConnectionHandler<TmqServerSocket, TmqMessage>
    {
        /// <summary>
        /// Default unique Id generator
        /// </summary>
        private readonly IUniqueIdGenerator _uniqueIdGenerator = new DefaultUniqueIdGenerator();

        /// <summary>
        /// User defined message received handler
        /// </summary>
        private readonly TmqMessageHandler _action;

        /// <summary>
        /// Creates new TMQ protocol handler for action-based message handling use
        /// </summary>
        /// <param name="action"></param>
        public TmqMethodHandler(TmqMessageHandler action)
        {
            _action = action;
        }

        /// <summary>
        /// Triggered when a TMQ client is connected. 
        /// </summary>
        public async Task<TmqServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            return await Task.FromResult(new TmqServerSocket(server, connection, _uniqueIdGenerator));
        }

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        public async Task Ready(ITwinoServer server, TmqServerSocket client)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Triggered when a TMQ message received from client
        /// </summary>
        public async Task Received(ITwinoServer server, IConnectionInfo info, TmqServerSocket client, TmqMessage message)
        {
            await _action(client, message);
        }

        /// <summary>
        /// Triggered when a TMQ client is connected. 
        /// </summary>
        public async Task Disconnected(ITwinoServer server, TmqServerSocket client)
        {
            await Task.CompletedTask;
        }
    }
}