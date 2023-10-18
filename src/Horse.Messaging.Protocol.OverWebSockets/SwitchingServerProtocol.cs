using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Protocol;
using Horse.WebSocket.Protocol;

namespace Horse.Messaging.Server.OverWebSockets;

internal class SwitchingServerProtocol : ISwitchingProtocol
{
    private readonly OverWsServerSocket _socket;
    private readonly WebSocketReader _wsReader = new WebSocketReader();

    public string ProtocolName => "websocket";

    internal SwitchingServerProtocol(OverWsServerSocket socket)
    {
        _socket = socket;
    }

    public void Ping()
    {
        _socket.Ping();
    }

    public void Pong(object pingMessage = null)
    {
        _socket.HorseClient.KeepAlive();
        _socket.Pong(pingMessage);
    }

    public bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        byte[] bytes = HorseProtocolWriter.Create(message, additionalHeaders);
        WebSocketMessage msg = new WebSocketMessage
        {
            OpCode = SocketOpCode.Binary,
            Content = new MemoryStream(bytes)
        };
        msg.Content.Position = 0;
        return _socket.Send(msg);
    }

    public Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        byte[] bytes = HorseProtocolWriter.Create(message, additionalHeaders);
        WebSocketMessage msg = new WebSocketMessage
        {
            OpCode = SocketOpCode.Binary,
            Content = new MemoryStream(bytes)
        };
        msg.Content.Position = 0;
        return _socket.SendAsync(msg);
    }

    public async Task<HorseMessage> Read(Stream stream)
    {
        WebSocketMessage wsMsg = await _wsReader.Read(stream);
        wsMsg.Content.Position = 0;
        HorseMessage message = await _socket.HorseReader.Read(wsMsg.Content);
        return message;
    }

    public Task ClientProtocolHandshake(ConnectionData data, Stream stream)
    {
        throw new InvalidOperationException("Switching Server Protocol does not support client side handshaking. Use SwitchingClientProtocol class instead.");
    }
}