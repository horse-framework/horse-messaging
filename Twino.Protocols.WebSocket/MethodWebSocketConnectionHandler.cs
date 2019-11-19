using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    public delegate Task WebSocketMessageRecievedHandler(WsServerSocket socket, WebSocketMessage message);

    internal class MethodWebSocketConnectionHandler : IProtocolConnectionHandler<WebSocketMessage>
    {
        private readonly WebSocketMessageRecievedHandler _handler;

        public MethodWebSocketConnectionHandler(WebSocketMessageRecievedHandler handler)
        {
            _handler = handler;
        }

        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            return await Task.FromResult(new WsServerSocket(server, connection));
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, WebSocketMessage message)
        {
            await _handler((WsServerSocket) client, message);
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }
    }
}