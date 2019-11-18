using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    public class TwinoWebSocketProtocol : ITwinoProtocol<WebSocketMessage>
    {
        public string Name => "websocket";

        public byte[] PingMessage => PredefinedMessages.PING;
        public byte[] PongMessage => PredefinedMessages.PONG;

        public IProtocolConnectionHandler<WebSocketMessage> Handler { get; }
        public ProtocolHandshakeResult HandshakeResult { get; private set; }

        private readonly ITwinoServer _server;

        public TwinoWebSocketProtocol(ITwinoServer server, IProtocolConnectionHandler<WebSocketMessage> handler)
        {
            _server = server;
            Handler = handler;
        }

        public async Task<ProtocolHandshakeResult> Handshake(IConnectionInfo info, byte[] data)
        {
            HandshakeResult = new ProtocolHandshakeResult();
            return await Task.FromResult(HandshakeResult);
        }

        public async Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, Dictionary<string, string> properties)
        {
            if (!info.Protocol.Name.Equals("http", StringComparison.InvariantCultureIgnoreCase))
                return await Task.FromResult(new ProtocolHandshakeResult());

            string key;
            bool hasKey = properties.TryGetValue(PredefinedMessages.WEBSOCKET_KEY, out key);
            if (!hasKey)
                return await Task.FromResult(new ProtocolHandshakeResult());

            ProtocolHandshakeResult result = new ProtocolHandshakeResult();
            result.PipeConnection = true;
            result.Accepted = true;
            result.ReadAfter = false;
            result.PreviouslyRead = null;
            result.Response = await CreateWebSocketHandshakeResponse(key);

            ServerSocketBase socket = await Handler.Connected(_server, info, properties);

            if (socket == null)
                return await Task.FromResult(new ProtocolHandshakeResult());

            void socketDisconnected(ServerSocketBase socketBase)
            {
                Handler.Disconnected(_server, info, socketBase);
                _server.Pinger.Remove(socket);
                socket.Disconnected -= socketDisconnected;
            }

            info.State = ConnectionStates.Pipe;
            socket.Disconnected += socketDisconnected;
            _server.Pinger.Add(socket);

            return result;
        }

        public async Task HandleConnection(IConnectionInfo info)
        {
            WebSocketReader reader = new WebSocketReader();
            while (info.Client != null && info.Client.Connected)
            {
                WebSocketMessage message = await reader.Read(info.GetStream());
                await Handler.Received(_server, info, message);
            }
        }

        public IProtocolMessageReader<WebSocketMessage> CreateReader()
        {
            return new WebSocketReader();
        }

        public IProtocolMessageWriter<WebSocketMessage> CreateWriter()
        {
            return new WebSocketWriter();
        }

        private static async Task<byte[]> CreateWebSocketHandshakeResponse(string websocketKey)
        {
            await using MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(PredefinedMessages.WEBSOCKET_101_SWITCHING_PROTOCOLS_CRLF);
            await ms.WriteAsync(PredefinedMessages.SERVER_CRLF);
            await ms.WriteAsync(PredefinedMessages.CONNECTION_UPGRADE_CRLF);
            await ms.WriteAsync(PredefinedMessages.UPGRADE_WEBSOCKET_CRLF);
            await ms.WriteAsync(PredefinedMessages.SEC_WEB_SOCKET_COLON);

            ReadOnlyMemory<byte> memory = Encoding.UTF8.GetBytes(CreateWebSocketGuid(websocketKey) + "\r\n\r\n");
            await ms.WriteAsync(memory);

            return ms.ToArray();
        }

        /// <summary>
        /// Computes response hash from the requested web socket key
        /// </summary>
        private static string CreateWebSocketGuid(string key)
        {
            byte[] keybytes = Encoding.UTF8.GetBytes(key + PredefinedMessages.WEBSOCKET_GUID);
            return Convert.ToBase64String(SHA1.Create().ComputeHash(keybytes));
        }
    }
}