using Twino.Client.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket.Connectors
{
    public class WsSingleMessageConnector : SingleMessageConnector<TwinoWebSocket, WebSocketMessage>
    {
    }
}