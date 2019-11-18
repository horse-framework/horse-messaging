using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    public static class WebSocketExtensions
    {
        public static ITwinoServer UseWebSockets(this ITwinoServer server, IProtocolConnectionHandler<WebSocketMessage> handler)
        {
            TwinoWebSocketProtocol protocol = new TwinoWebSocketProtocol(server, handler);
            server.UseProtocol(protocol);
            return server;
        }
    }
}