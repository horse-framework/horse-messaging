using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    /// <summary>
    /// Necessity connector for websocket.
    /// </summary>
    public class WsNecessityConnector : NecessityConnector<TwinoWebSocket, WebSocketMessage>
    {
    }
}