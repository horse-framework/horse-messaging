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
            result.Response = PredefinedMessages.PROTOCOL_BYTES;
            result.PipeConnection = true;

            return result;
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

                await _handler.Received(_server, info, handshakeResult.Socket, message);
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