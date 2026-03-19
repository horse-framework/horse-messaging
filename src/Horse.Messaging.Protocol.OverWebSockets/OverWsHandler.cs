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

        // Read the first WebSocket frame from the client — this should be the HELLO message.
        // This mirrors HorseProtocol.ProcessFirstMessage(): the client sends HELLO with
        // CLIENT_ID, CLIENT_NAME, CLIENT_TYPE etc. right after the handshake.
        // Without this, HorseNetworkHandler.Connected() receives no CLIENT_ID and generates
        // a random one, causing an ID mismatch between client and server.
        await ReadHelloIntoData(socket);

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

    /// <summary>
    /// Reads the first WebSocket message (expected: HELLO) and merges its properties
    /// into socket.Data so that HorseNetworkHandler.Connected() receives CLIENT_ID,
    /// CLIENT_NAME, CLIENT_TYPE and other connection metadata.
    /// If the first message is not a HELLO or reading fails, socket.Data is unchanged
    /// and the server will fall back to generating a random client ID.
    /// </summary>
    private async Task ReadHelloIntoData(OverWsServerSocket socket)
    {
        try
        {
            WebSocketReader wsReader = new WebSocketReader(null);
            WebSocketMessage wsMsg = await wsReader.Read(socket.Info.GetStream());

            if (wsMsg?.Content == null)
                return;

            wsMsg.Content.Position = 0;
            HorseMessage helloMessage = await socket.HorseReader.Read(wsMsg.Content);

            if (helloMessage == null)
                return;

            if (helloMessage.Type != MessageType.Server || helloMessage.ContentType != KnownContentTypes.Hello)
                return;

            if (helloMessage.Content == null || helloMessage.Content.Length == 0)
                return;

            helloMessage.Content.Position = 0;
            ConnectionData helloData = new ConnectionData();
            await helloData.ReadFromStream(helloMessage.Content);

            foreach (var kv in helloData.Properties)
                socket.Data.Properties[kv.Key] = kv.Value;

            if (!string.IsNullOrEmpty(helloData.Method))
                socket.Data.Method = helloData.Method;

            if (!string.IsNullOrEmpty(helloData.Path))
                socket.Data.Path = helloData.Path;
        }
        catch
        {
            // If reading fails for any reason, fall back to the original behavior
            // (server generates a random ID). Don't break the connection.
        }
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
