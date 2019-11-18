using System;
using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    public class WsAbsoluteConnector : AbsoluteConnector<TwinoWebSocket, WebSocketMessage>
    {
        public WsAbsoluteConnector(TimeSpan reconnectInterval) : base(reconnectInterval)
        {
        }
    }
}