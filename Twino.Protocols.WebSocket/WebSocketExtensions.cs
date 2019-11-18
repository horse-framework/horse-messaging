using System.Net.Http;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.Protocols.Http;

namespace Twino.Protocols.WebSocket
{
    public static class WebSocketExtensions
    {
        public static ITwinoServer UseWebSockets(this ITwinoServer server, IProtocolConnectionHandler<WebSocketMessage> handler)
        {
            //we need http protocol is added
            ITwinoProtocol http = server.FindProtocol("http");
            if (http == null)
            {
                TwinoHttpProtocol httpProtocol = new TwinoHttpProtocol(server, HttpClientHandler, null);
                server.UseProtocol(httpProtocol);
            }
            
            TwinoWebSocketProtocol protocol = new TwinoWebSocketProtocol(server, handler);
            server.UseProtocol(protocol);
            return server;
        }
    }
}