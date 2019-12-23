using Twino.Core;
using Twino.Core.Protocols;
using Twino.Protocols.Http;

namespace Twino.Protocols.WebSocket
{
    public static class WebSocketExtensions
    {
        /// <summary>
        /// Uses WebSocket Protocol and accepts HTTP connections which comes with "Upgrade: websocket" header data
        /// </summary>
        public static ITwinoServer UseWebSockets(this ITwinoServer server,
                                                 IProtocolConnectionHandler<WsServerSocket,
                                                     WebSocketMessage> handler)
        {
            return UseWebSockets(server, handler, HttpOptions.CreateDefault());
        }

        /// <summary>
        /// Uses WebSocket Protocol and accepts HTTP connections which comes with "Upgrade: websocket" header data
        /// </summary>
        public static ITwinoServer UseWebSockets(this ITwinoServer server,
                                                 WebSocketMessageRecievedHandler handlerAction)
        {
            return UseWebSockets(server, new MethodWebSocketConnectionHandler(handlerAction), HttpOptions.CreateDefault());
        }

        /// <summary>
        /// Uses WebSocket Protocol and accepts HTTP connections which comes with "Upgrade: websocket" header data
        /// </summary>
        public static ITwinoServer UseWebSockets(this ITwinoServer server,
                                                 WebSocketMessageRecievedHandler handlerAction,
                                                 HttpOptions options)
        {
            return UseWebSockets(server, new MethodWebSocketConnectionHandler(handlerAction), options);
        }

        /// <summary>
        /// Uses WebSocket Protocol and accepts HTTP connections which comes with "Upgrade: websocket" header data
        /// </summary>
        public static ITwinoServer UseWebSockets(this ITwinoServer server,
                                                 IProtocolConnectionHandler<WsServerSocket, WebSocketMessage> handler,
                                                 HttpOptions options)
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

        /// <summary>
        /// Uses WebSocket Protocol and accepts HTTP connections which comes with "Upgrade: websocket" header data
        /// </summary>
        public static ITwinoServer UseWebSockets(this ITwinoServer server,
                                                 WebSocketConnectedHandler connectedAction,
                                                 WebSocketMessageRecievedHandler messageAction)
        {
            return UseWebSockets(server,
                                 new MethodWebSocketConnectionHandler(connectedAction, messageAction),
                                 HttpOptions.CreateDefault());
        }
    }
}