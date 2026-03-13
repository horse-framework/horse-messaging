using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;
using Horse.Messaging.Protocol;
using Horse.WebSocket.Protocol;

namespace Horse.Messaging.Server.OverWebSockets;

internal class OverWsHandler : IProtocolConnectionHandler<WsServerSocket, WebSocketMessage>
{
    private readonly IProtocolConnectionHandler<HorseServerSocket, HorseMessage> _horseHandler;

    public OverWsHandler(IProtocolConnectionHandler<HorseServerSocket, HorseMessage> horseHandler)
    {
        _horseHandler = horseHandler;
    }

    public Task<WsServerSocket> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data)
    {
        data.Properties.Add(HorseHeaders.UNDERLYING_PROTOCOL, "websocket");
        
        OverWsServerSocket socket = new OverWsServerSocket(server, connection);
        socket.Data = data;
        return Task.FromResult<WsServerSocket>(socket);
    }

    public async Task Ready(IHorseServer server, WsServerSocket client)
    {
        OverWsServerSocket socket = (OverWsServerSocket) client;
        HorseServerSocket horseSocket = await _horseHandler.Connected(server, socket.Info, socket.Data);
        
        ISwitchingProtocolClient switchingClient = horseSocket as ISwitchingProtocolClient;
        if (switchingClient == null)
        {
            socket.Disconnect();
            return;
        }

        socket.HorseClient = switchingClient;
        socket.ServerSocket = horseSocket;
        switchingClient.SwitchingProtocol = new SwitchingServerProtocol(socket);
        await _horseHandler.Ready(server, socket.ServerSocket);
    }

    public async Task Received(IHorseServer server, IConnectionInfo info, WsServerSocket client, WebSocketMessage message)
    {
        OverWsServerSocket socket = (OverWsServerSocket) client;
        message.Content.Position = 0;
        HorseMessage horseMessage = await socket.HorseReader.Read(message.Content);
        await _horseHandler.Received(server, info, socket.ServerSocket, horseMessage);
    }

    public Task Disconnected(IHorseServer server, WsServerSocket client)
    {
        OverWsServerSocket socket = (OverWsServerSocket) client;
        return _horseHandler.Disconnected(server, socket.ServerSocket);
    }
}