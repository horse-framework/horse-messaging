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
        public WsSingleMessageConnector(Func<TwinoWebSocket> createInstance = null)
            : base(createInstance)
        {
        }
    }
}