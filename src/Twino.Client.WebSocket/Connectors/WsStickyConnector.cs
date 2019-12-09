using System;
using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    /// <summary>
    /// Sticky connector for websocket.
    /// </summary>
    public class WsStickyConnector : StickyConnector<TwinoWebSocket, WebSocketMessage>
    {
        public WsStickyConnector(TimeSpan reconnectInterval, Func<TwinoWebSocket> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
        }
    }
}