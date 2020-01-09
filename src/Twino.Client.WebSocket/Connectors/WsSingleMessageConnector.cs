using System;
using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    /// <summary>
    /// Single message connector for websocket.
    /// </summary>
    public class WsSingleMessageConnector : SingleMessageConnector<TwinoWebSocket, WebSocketMessage>
    {
        /// <summary>
        /// Creates new single message connector for websocket connections
        /// </summary>
        public WsSingleMessageConnector(Func<TwinoWebSocket> createInstance = null)
            : base(createInstance)
        {
        }
    }
}