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

    public async Task<WsServerSocket> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data)
    {
        OverWsServerSocket socket = new OverWsServerSocket(server, connection);

        //todo: set client information

        HorseServerSocket horseSocket = await _horseHandler.Connected(server, connection, data);

        ISwitchingProtocolClient switchingClient = horseSocket as ISwitchingProtocolClient;
        if (switchingClient == null)
            return null;

        switchingClient.SwitchingProtocol = new SwitchingServerProtocol(socket);
        socket.HorseClient = switchingClient;
        socket.ServerSocket = horseSocket;

        return socket;
    }

    public Task Ready(IHorseServer server, WsServerSocket client)
    {
        OverWsServerSocket socket = (OverWsServerSocket) client;
        _horseHandler.Ready(server, socket.ServerSocket);
        return Task.CompletedTask;
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