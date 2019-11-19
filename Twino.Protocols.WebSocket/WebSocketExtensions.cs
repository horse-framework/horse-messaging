using Twino.Core;
using Twino.Core.Protocols;
using Twino.Protocols.Http;

namespace Twino.Protocols.WebSocket
{
    public static class WebSocketExtensions
    {
        public static ITwinoServer UseWebSockets(this ITwinoServer server, IProtocolConnectionHandler<WebSocketMessage> handler)
        {
            return UseWebSockets(server, handler, HttpOptions.CreateDefault());
        }

        public static ITwinoServer UseWebSockets(this ITwinoServer server, WebSocketMessageRecievedHandler handlerAction)
        {
            return UseWebSockets(server, new MethodWebSocketConnectionHandler(handlerAction), HttpOptions.CreateDefault());
        }

        public static ITwinoServer UseWebSockets(this ITwinoServer server, WebSocketMessageRecievedHandler handlerAction, HttpOptions options)
        {
            return UseWebSockets(server, new MethodWebSocketConnectionHandler(handlerAction), options);
        }

        public static ITwinoServer UseWebSockets(this ITwinoServer server, IProtocolConnectionHandler<WebSocketMessage> handler, HttpOptions options)
        {
            //we need http protocol is added
            ITwinoProtocol http = server.FindProtocol("http");
            if (http == null)
            {
                TwinoHttpProtocol httpProtocol = new TwinoHttpProtocol(server, new WebSocketHttpHandler(), options);
                server.UseProtocol(httpProtocol);
            }

            TwinoWebSocketProtocol protocol = new TwinoWebSocketProtocol(server, handler);
            server.UseProtocol(protocol);
            return server;
        }
    }
}