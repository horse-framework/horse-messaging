using System;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// Twino protocol class for TMQ Protocol
    /// </summary>
    public class TwinoTmqProtocol : ITwinoProtocol
    {
        /// <summary>
        /// Protocol name: tmq
        /// </summary>
        public string Name => "tmq";

        /// <summary>
        /// Protocol connection handler
        /// </summary>
        private readonly IProtocolConnectionHandler<TmqMessage> _handler;

        /// <summary>
        /// Server object
        /// </summary>
        private readonly ITwinoServer _server;

        public TwinoTmqProtocol(ITwinoServer server, IProtocolConnectionHandler<TmqMessage> handler)
        {
            _server = server;
            _handler = handler;
        }

        /// <summary>
        /// Checks if received data is a TMQ protocol message
        /// </summary>
        public async Task<ProtocolHandshakeResult> Handshake(IConnectionInfo info, byte[] data)
        {
            ProtocolHandshakeResult result = new ProtocolHandshakeResult();

            if (data.Length < 8)
                return await Task.FromResult(result);

            result.Accepted = CheckProtocol(data);
            if (!result.Accepted)
                return result;

            TmqReader reader = new TmqReader();
            TmqMessage message = await reader.Read(info.GetStream());

            //sends protocol message
            await info.GetStream().WriteAsync(PredefinedMessages.PROTOCOL_BYTES);

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
        private async Task<bool> ProcessFirstMessage(TmqMessage message, IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
        {
            if (message.Type != MessageType.Server || message.ContentType != KnownContentTypes.Hello)
                return false;

            ConnectionData connectionData = new ConnectionData();
            message.Content.Position = 0;
            await connectionData.ReadFromStream(message.Content);

            SocketBase socket = await _handler.Connected(_server, info, connectionData);
            if (socket == null)
            {
                info.Close();
                return false;
            }

            void socketDisconnected(SocketBase socketBase)
            {
                _handler.Disconnected(_server, socketBase);
                _server.Pinger.Remove(socket);
                socket.Disconnected -= socketDisconnected;
            }

            handshakeResult.Socket = socket;
            info.State = ConnectionStates.Pipe;
            socket.Disconnected += socketDisconnected;
            _server.Pinger.Add(socket);
            return true;
        }

        /// <summary>
        /// Switching protocols to TMQ is not supported
        /// </summary>
        public Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, ConnectionData data)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Handles the connection and reads received TMQ messages
        /// </summary>
        public async Task HandleConnection(IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
        {
            //if user makes a mistake in ready method, we should not interrupt connection handling
            try
            {
                await _handler.Ready(_server, handshakeResult.Socket);
            }
            catch (Exception e)
            {
                if (_server.Logger != null)
                    _server.Logger.LogException("Unhandled Exception", e);
            }

            TmqReader reader = new TmqReader();

            while (info.Client != null && info.Client.Connected)
            {
                TmqMessage message = await reader.Read(info.GetStream());
                if (message == null)
                {
                    info.Close();
                    return;
                }

                if (message.Ttl < 0)
                    continue;

                //if user makes a mistake in received method, we should not interrupt connection handling
                try
                {
                    await _handler.Received(_server, info, handshakeResult.Socket, message);
                }
                catch (Exception e)
                {
                    if (_server.Logger != null)
                        _server.Logger.LogException("Unhandled Exception", e);
                }
            }
        }

        /// <summary>
        /// Checks data if TMQ protocol data
        /// </summary>
        private static bool CheckProtocol(byte[] data)
        {
            ReadOnlySpan<byte> span = data;
            return span.StartsWith(PredefinedMessages.PROTOCOL_BYTES);
        }
    }
}