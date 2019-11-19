using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.TMQ
{
    public class TwinoTmqProtocol : ITwinoProtocol<TmqMessage>
    {
        public string Name => "tmq";
        public byte[] PingMessage => PredefinedMessages.PING;
        public byte[] PongMessage => PredefinedMessages.PONG;
        public IProtocolConnectionHandler<TmqMessage> Handler { get; }
        private readonly ITwinoServer _server;

        public TwinoTmqProtocol(ITwinoServer server, IProtocolConnectionHandler<TmqMessage> handler)
        {
            _server = server;
            Handler = handler;
        }

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

        public Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, ConnectionData data)
        {
            throw new NotSupportedException();
        }

        public async Task HandleConnection(IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
        {
            TmqReader reader = new TmqReader();
            while (info.Client != null && info.Client.Connected)
            {
                TmqMessage message = await reader.Read(info.GetStream());
                await Handler.Received(_server, info, handshakeResult.Socket, message);
            }
        }

        private static bool CheckProtocol(byte[] data)
        {
            ReadOnlySpan<byte> span = data;
            return span.StartsWith(PredefinedMessages.PROTOCOL_BYTES);
        }

        public IProtocolMessageReader<TmqMessage> CreateReader()
        {
            return new TmqReader();
        }

        public IProtocolMessageWriter<TmqMessage> CreateWriter()
        {
            return new TmqWriter();
        }
    }
}