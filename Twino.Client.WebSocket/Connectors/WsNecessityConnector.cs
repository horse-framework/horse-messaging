using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    public class WsNecessityConnector : NecessityConnector<TwinoWebSocket, WebSocketMessage>
    {
    }
}