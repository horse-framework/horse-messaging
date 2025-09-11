using Horse.Core;
using Horse.Messaging.Protocol;
using Horse.WebSocket.Protocol;

namespace Horse.Messaging.Server.OverWebSockets;

internal class OverWsServerSocket : WsServerSocket
{
    internal HorseServerSocket ServerSocket { get; set; }
    internal ISwitchingProtocolClient HorseClient { get; set; }
    internal ConnectionData Data { get; set; }
    internal HorseProtocolReader HorseReader { get; set; }
    
    public OverWsServerSocket(IHorseServer server, IConnectionInfo info) : base(server, info, null)
    {
        HorseReader = new HorseProtocolReader();
    }
}