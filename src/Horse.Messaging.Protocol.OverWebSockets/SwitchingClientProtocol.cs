using System.Security.Cryptography;
using System.Text;
using Horse.Core;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Protocols.Http;
using Horse.WebSocket.Protocol;
using PredefinedMessages = Horse.WebSocket.Protocol.PredefinedMessages;

namespace Horse.Messaging.Server.OverWebSockets;

internal class SwitchingClientProtocol : ISwitchingProtocol
{
    private string _websocketKey;
    private readonly HorseClient _client;
    private readonly WebSocketReader _reader = new WebSocketReader();
    private readonly WebSocketWriter _writer = new WebSocketWriter();
    private readonly HorseProtocolReader _horseReader = new HorseProtocolReader();

    internal SwitchingClientProtocol(HorseClient client)
    {
        _client = client;
    }

    public void Ping()
    {
        _client.SendRaw(PredefinedMessages.PING);
    }

    public void Pong(object pingMessage = null)
    {
        if (pingMessage == null)
        {
            _client.SendRaw(PredefinedMessages.PONG);
            return;
        }

        WebSocketMessage ping = pingMessage as WebSocketMessage;
        if (ping == null)
        {
            _client.SendRaw(PredefinedMessages.PONG);
            return;
        }

        WebSocketMessage pong = new WebSocketMessage();
        pong.OpCode = SocketOpCode.Pong;
        pong.Masking = ping.Masking;
        if (ping.Length > 0)
            pong.Content = new MemoryStream(ping.Content.ToArray());

        byte[] data = new WebSocketWriter().Create(pong, null);
        _client.SendRaw(data);
    }

    public bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        byte[] bytes = HorseProtocolWriter.Create(message, additionalHeaders);
        WebSocketMessage msg = new WebSocketMessage
        {
            OpCode = SocketOpCode.Binary,
            Content = new MemoryStream(bytes)
        };

        return _client.SendRaw(_writer.Create(msg));
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
        return _client.SendRawAsync(_writer.Create(msg));
    }

    public async Task<HorseMessage> Read(Stream stream)
    {
        WebSocketMessage wsMsg = await _reader.Read(stream);
        
        if (wsMsg == null)
            return null;
        
        wsMsg.Content.Position = 0;
        HorseMessage message = await _horseReader.Read(wsMsg.Content);
        return message;
    }

    public Task ClientProtocolHandshake(ConnectionData data, Stream stream)
    {
        byte[] request = CreateRequest(data);
        stream.Write(request, 0, request.Length);

        byte[] buffer = new byte[8192];
        int len = stream.Read(buffer, 0, buffer.Length);
        CheckProtocolResponse(buffer, len);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates HTTP Request for well defined remote host
    /// </summary>
    private byte[] CreateRequest(ConnectionData data)
    {
        using (SHA1 sha1 = SHA1.Create())
        {
            byte[] hash = sha1.ComputeHash(Guid.NewGuid().ToByteArray());
            _websocketKey = Convert.ToBase64String(hash);
        }

        data.Properties.TryGetValue("PATH", out string path);
        if (string.IsNullOrEmpty(path))
            path = "/";

        string request = HttpHeaders.HTTP_GET + " " + path + " " + HttpHeaders.HTTP_VERSION + "\r\n" +
                         HttpHeaders.Create(HttpHeaders.CONNECTION, HttpHeaders.UPGRADE) +
                         HttpHeaders.Create(HttpHeaders.PRAGMA, HttpHeaders.VALUE_NO_CACHE) +
                         HttpHeaders.Create(HttpHeaders.CACHE_CONTROL, HttpHeaders.VALUE_NO_CACHE) +
                         HttpHeaders.Create(HttpHeaders.UPGRADE, HttpHeaders.VALUE_WEBSOCKET) +
                         HttpHeaders.Create(HttpHeaders.WEBSOCKET_VERSION, HttpHeaders.VALUE_WEBSOCKET_VERSION) +
                         HttpHeaders.Create(HttpHeaders.ACCEPT_ENCODING, HttpHeaders.VALUE_GZIP_DEFLATE_BR) +
                         HttpHeaders.Create(HttpHeaders.ACCEPT_LANGUAGE, HttpHeaders.VALUE_ACCEPT_EN) +
                         HttpHeaders.Create(HttpHeaders.WEBSOCKET_KEY, _websocketKey) +
                         HttpHeaders.Create(HttpHeaders.WEBSOCKET_EXTENSIONS, HttpHeaders.VALUE_WEBSOCKET_EXTENSIONS);

        foreach (var kv in data.Properties)
            request += HttpHeaders.Create(kv.Key, kv.Value);

        request += "\r\n";
        return Encoding.UTF8.GetBytes(request);
    }

    /// <summary>
    /// Checks if data is a valid protocol message
    /// </summary>
    private void CheckProtocolResponse(byte[] buffer, int length)
    {
        string response = Encoding.UTF8.GetString(buffer, 0, length);

        string first = response.Substring(0, 50).Trim();
        int i1 = first.IndexOf(' ');
        if (i1 < 1)
            throw new InvalidOperationException("Unexpected server response");

        int i2 = first.IndexOf(' ', i1 + 1);
        if (i1 < 0 || i2 < 0 || i2 <= i1)
            throw new InvalidOperationException("Unexpected server response");

        string statusCode = first.Substring(i1, i2 - i1).Trim();
        if (statusCode != "101")
            throw new InvalidOperationException("Connection Error: " + statusCode);

        string[] responseLines = response.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
        string acceptLine = responseLines.FirstOrDefault(x => x.StartsWith(HttpHeaders.WEBSOCKET_ACCEPT, StringComparison.InvariantCultureIgnoreCase));

        if (acceptLine == null)
            throw new InvalidOperationException("Handshaking error, server didn't response Sec-WebSocket-Accept");

        string[] pair = acceptLine.Split(':');
        string responseKey = pair[1].Trim();

        //check if the key is valid
        using SHA1 sha1 = SHA1.Create();
        byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(_websocketKey + HttpHeaders.WEBSOCKET_GUID));
        string fkey = Convert.ToBase64String(hash);

        if (responseKey != fkey)
            throw new InvalidOperationException("Handshaking error, Invalid Key");
    }
}