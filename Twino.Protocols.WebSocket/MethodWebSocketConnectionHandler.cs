using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    public delegate Task WebSocketMessageRecievedHandler(WsServerSocket socket, WebSocketMessage message);

    /// <summary>
    /// Twino WebSocket Server with WebSocketMessageRecievedHandler method implementation handler
    /// </summary>
    internal class MethodWebSocketConnectionHandler : IProtocolConnectionHandler<WebSocketMessage>
    {
        /// <summary>
        /// User defined action
        /// </summary>
        private readonly WebSocketMessageRecievedHandler _handler;

        public MethodWebSocketConnectionHandler(WebSocketMessageRecievedHandler handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Triggered when a websocket client is connected. 
        /// </summary>
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            return await Task.FromResult(new WsServerSocket(server, connection));
        }

        /// <summary>
        /// Triggered when a client sends a message to the server 
        /// </summary>
        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, WebSocketMessage message)
        {
            await _handler((WsServerSocket) client, message);
        }

        /// <summary>
        /// Triggered when a websocket client is disconnected. 
        /// </summary>
        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }
    }
}