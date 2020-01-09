using System;
using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    /// <summary>
    /// Necessity connector for websocket.
    /// </summary>
    public class WsNecessityConnector : NecessityConnector<TwinoWebSocket, WebSocketMessage>
    {
        /// <summary>
        /// Creates new necessity connector for websocket connections
        /// </summary>
        public WsNecessityConnector(Func<TwinoWebSocket> createInstance = null)
            : base(createInstance)
        {
        }
    }
}