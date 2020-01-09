using System;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    /// <summary>
    /// Absolute connector for websocket.
    /// </summary>
    public class WsAbsoluteConnector : AbsoluteConnector<TwinoWebSocket, WebSocketMessage>
    {
        /// <summary>
        /// Creates new absolute connector for websocket connections
        /// </summary>
        public WsAbsoluteConnector(TimeSpan reconnectInterval, Func<TwinoWebSocket> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
        }
    }
}