using System;
using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    public class WsStickyConnector : StickyConnector<TwinoWebSocket, WebSocketMessage>
    {
        public WsStickyConnector(TimeSpan reconnectInterval) : base(reconnectInterval)
        {
        }
    }
}