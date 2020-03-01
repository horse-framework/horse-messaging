using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    /// <summary>
    /// Websocket message received handler for server-side sockets
    /// </summary>
    public delegate Task WebSocketMessageRecievedHandler(WsServerSocket socket, WebSocketMessage message);

    /// <summary>
    /// Websocket connected handler for server-side sockets
    /// </summary>
    public delegate Task WebSocketConnectedHandler(WsServerSocket socket, ConnectionData data);

    /// <summary>
    /// Websocket handshake is completed and it's ready to send and receive messages
    /// </summary>
    public delegate Task WebSocketReadyHandler(WsServerSocket socket);

    /// <summary>
    /// Twino WebSocket Server with WebSocketMessageRecievedHandler method implementation handler
    /// </summary>
    internal class MethodWebSocketConnectionHandler : IProtocolConnectionHandler<WsServerSocket, WebSocketMessage>
    {
        /// <summary>
        /// User defined action
        /// </summary>
        private readonly WebSocketMessageRecievedHandler _messageHandler;

        private readonly WebSocketConnectedHandler _connectedHandler;
        private readonly WebSocketReadyHandler _readyHandler;

        public MethodWebSocketConnectionHandler(WebSocketMessageRecievedHandler handler)
            : this(null, null, handler)
        {
        }

        public MethodWebSocketConnectionHandler(WebSocketConnectedHandler connectedHandler, WebSocketReadyHandler readyHandler, WebSocketMessageRecievedHandler messageHandler)
        {
            _connectedHandler = connectedHandler;
            _readyHandler = readyHandler;
            _messageHandler = messageHandler;
        }

        /// <summary>
        /// Triggered when a websocket client is connected. 
        /// </summary>
        public async Task<WsServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            WsServerSocket socket = new WsServerSocket(server, connection);

            if (_connectedHandler != null)
                await _connectedHandler(socket, data);

            return socket;
        }

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        public async Task Ready(ITwinoServer server, WsServerSocket client)
        {
            if (_readyHandler != null)
                await _readyHandler(client);
        }

        /// <summary>
        /// Triggered when a client sends a message to the server 
        /// </summary>
        public async Task Received(ITwinoServer server, IConnectionInfo info, WsServerSocket client, WebSocketMessage message)
        {
            await _messageHandler(client, message);
        }

        /// <summary>
        /// Triggered when a websocket client is disconnected. 
        /// </summary>
        public async Task Disconnected(ITwinoServer server, WsServerSocket client)
        {
            await Task.CompletedTask;
        }
    }
}