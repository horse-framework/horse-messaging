using System;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;

namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// Horse protocol class for Horse Protocol
    /// </summary>
    public class HorseProtocol : IHorseProtocol
    {
        /// <summary>
        /// Protocol name: horse
        /// </summary>
        public string Name => "horse";

        /// <summary>
        /// Protocol connection handler
        /// </summary>
        private readonly IProtocolConnectionHandler<HorseServerSocket, HorseMessage> _handler;

        /// <summary>
        /// Server object
        /// </summary>
        private readonly IHorseServer _server;

        /// <summary>
        /// Creates new Horse Protocol handler
        /// </summary>
        public HorseProtocol(IHorseServer server, IProtocolConnectionHandler<HorseServerSocket, HorseMessage> handler)
        {
            _server = server;
            _handler = handler;
        }

        /// <summary>
        /// Checks if received data is a Horse protocol message
        /// </summary>
        public async Task<ProtocolHandshakeResult> Handshake(IConnectionInfo info, byte[] data)
        {
            ProtocolHandshakeResult result = new ProtocolHandshakeResult();

            if (data.Length < 8)
                return await Task.FromResult(result);

            ProtocolVersion version = CheckProtocol(data);
            result.Accepted = version != ProtocolVersion.Unknown;
            if (!result.Accepted)
                return result;

            HorseProtocolReader reader = new HorseProtocolReader();
            HorseMessage message = await reader.Read(info.GetStream());

            //sends protocol message
            await info.GetStream().WriteAsync(PredefinedMessages.PROTOCOL_BYTES_V3);

            bool alive = await ProcessFirstMessage(message, info, result);
            if (!alive)
                return result;

            result.PipeConnection = true;
            info.State = ConnectionStates.Pipe;
            info.Protocol = this;

            return result;
        }

        /// <summary>
        /// Reads first Hello message from client
        /// </summary>
        private async Task<bool> ProcessFirstMessage(HorseMessage message, IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
        {
            if (message.Type != MessageType.Server || message.ContentType != KnownContentTypes.Hello)
                return false;

            ConnectionData connectionData = new ConnectionData();
            message.Content.Position = 0;
            await connectionData.ReadFromStream(message.Content);

            HorseServerSocket socket = await _handler.Connected(_server, info, connectionData);
            if (socket == null)
            {
                info.Close();
                return false;
            }

            info.State = ConnectionStates.Pipe;
            handshakeResult.Socket = socket;
            _server.HeartbeatManager?.Add(socket);

            socket.SetCleanupAction(s =>
            {
                _server.HeartbeatManager?.Remove(socket);
                _handler.Disconnected(_server, s);
            });

            return true;
        }

        /// <summary>
        /// Switching protocols to Horse is not supported
        /// </summary>
        public Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, ConnectionData data)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Handles the connection and reads received Horse messages
        /// </summary>
        public async Task HandleConnection(IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
        {
            //if user makes a mistake in ready method, we should not interrupt connection handling
            try
            {
                await _handler.Ready(_server, (HorseServerSocket) handshakeResult.Socket);
            }
            catch (Exception e)
            {
                if (_server.Logger != null)
                    _server.Logger.LogException("Unhandled Exception", e);
            }

            HorseProtocolReader reader = new HorseProtocolReader();

            while (info.Client != null && info.Client.Connected)
            {
                HorseMessage message = await reader.Read(info.GetStream());
                if (message == null)
                {
                    info.Close();
                    return;
                }

                await ProcessMessage(info, message, (HorseServerSocket) handshakeResult.Socket);
            }
        }

        private Task ProcessMessage(IConnectionInfo info, HorseMessage message, HorseServerSocket socket)
        {
            //if user makes a mistake in received method, we should not interrupt connection handling
            try
            {
                if (socket.SmartHealthCheck)
                    socket.KeepAlive();
                else if (message.Type == MessageType.Pong)
                    socket.KeepAlive();

                return _handler.Received(_server, info, socket, message);
            }
            catch (Exception e)
            {
                if (_server.Logger != null)
                    _server.Logger.LogException("Unhandled Exception", e);

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Checks data if Horse protocol data
        /// </summary>
        private static ProtocolVersion CheckProtocol(byte[] data)
        {
            ReadOnlySpan<byte> span = data;
            bool v2 = span.StartsWith(PredefinedMessages.PROTOCOL_BYTES_V3);
            if (v2)
                return ProtocolVersion.Version2;

            return ProtocolVersion.Unknown;
        }
    }
}