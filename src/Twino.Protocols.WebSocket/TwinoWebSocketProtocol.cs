using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    /// <summary>
    /// Twino WebSocket Server protocol
    /// </summary>
    public class TwinoWebSocketProtocol : ITwinoProtocol
    {
        public string Name => "websocket";

        /// <summary>
        /// WebSocket protocol connection handler
        /// </summary>
        private readonly IProtocolConnectionHandler<WsServerSocket, WebSocketMessage> _handler;

        /// <summary>
        /// Twino server
        /// </summary>
        private readonly ITwinoServer _server;

        public TwinoWebSocketProtocol(ITwinoServer server, IProtocolConnectionHandler<WsServerSocket, WebSocketMessage> handler)
        {
            _server = server;
            _handler = handler;
        }

        /// <summary>
        /// Checks if data is belong this protocol.
        /// </summary>
        /// <param name="info">Connection information</param>
        /// <param name="data">Data is first 8 bytes of the first received message from the client</param>
        /// <returns></returns>
        public async Task<ProtocolHandshakeResult> Handshake(IConnectionInfo info, byte[] data)
        {
            return await Task.FromResult(new ProtocolHandshakeResult());
        }

        /// <summary>
        /// When protocol is switched to this protocol from another protocol
        /// </summary>
        public async Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, ConnectionData data)
        {
            if (!info.Protocol.Name.Equals("http", StringComparison.InvariantCultureIgnoreCase))
                return await Task.FromResult(new ProtocolHandshakeResult());

            string key;
            bool hasKey = data.Properties.TryGetValue(PredefinedMessages.WEBSOCKET_KEY, out key);
            if (!hasKey)
                return await Task.FromResult(new ProtocolHandshakeResult());

            ProtocolHandshakeResult result = new ProtocolHandshakeResult();
            result.PipeConnection = true;
            result.Accepted = true;
            result.ReadAfter = false;
            result.PreviouslyRead = null;
            result.Response = await CreateWebSocketHandshakeResponse(key);

            WsServerSocket socket = await _handler.Connected(_server, info, data);

            if (socket == null)
                return await Task.FromResult(new ProtocolHandshakeResult());

            info.State = ConnectionStates.Pipe;
            result.Socket = socket;
            _server.Pinger.Add(socket);

            socket.SetCleanupAction(s =>
            {
                _server.Pinger.Remove(socket);
                _handler.Disconnected(_server, s);
            });

            return result;
        }

        /// <summary>
        /// After protocol handshake is completed, this method is called to handle events for the specified client
        /// </summary>
        public async Task HandleConnection(IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
        {
            //if user makes a mistake in ready method, we should not interrupt connection handling
            try
            {
                await _handler.Ready(_server, (WsServerSocket) handshakeResult.Socket);
            }
            catch (Exception e)
            {
                if (_server.Logger != null)
                    _server.Logger.LogException("Unhandled Exception", e);
            }

            WebSocketReader reader = new WebSocketReader();
            Stream stream = info.GetStream();
            while (info.Socket != null && info.Socket.IsConnected)
            {
                WebSocketMessage message = await reader.Read(stream);

                if (message == null)
                {
                    info.Close();
                    return;
                }

                await ProcessMessage(info, handshakeResult.Socket, message);
            }
        }

        /// <summary>
        /// Creates websocket response protocol message
        /// </summary>
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

        /// <summary>
        /// Process websocket message
        /// </summary>
        private async Task ProcessMessage(IConnectionInfo info, SocketBase socket, WebSocketMessage message)
        {
            switch (message.OpCode)
            {
                case SocketOpCode.Binary:
                case SocketOpCode.UTF8:
                    //if user makes a mistake in received method, we should not interrupt connection handling
                    try
                    {
                        await _handler.Received(_server, info, (WsServerSocket) socket, message);
                    }
                    catch (Exception e)
                    {
                        if (_server.Logger != null)
                            _server.Logger.LogException("Unhandled Exception", e);
                    }

                    break;

                //close the connection if terminate requested
                case SocketOpCode.Terminate:
                    info.Close();
                    break;
                
                //if client sends a ping message, response with pong
                case SocketOpCode.Ping:
                    await socket.SendAsync(PredefinedMessages.PONG);
                    break;

                //client sent response pong to ping message
                case SocketOpCode.Pong:
                    info.PongReceived();
                    break;
            }
        }
    }
}