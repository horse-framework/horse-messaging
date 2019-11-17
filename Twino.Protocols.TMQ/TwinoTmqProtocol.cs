using System;
using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Protocols.TMQ
{
    public class TwinoTmqProtocol : ITwinoProtocol<TmqMessage>
    {
        public string Name => "tmq";
        public byte[] PingMessage => PredefinedMessages.PING;
        public byte[] PongMessage => PredefinedMessages.PONG;

        public async Task<ProtocolHandshakeResult> Check(byte[] data)
        {
            ProtocolHandshakeResult result = new ProtocolHandshakeResult();
            if (data.Length < 8)
                return await Task.FromResult(result);

            result.Accepted = CheckProtocol(data);
            result.Response = PredefinedMessages.PROTOCOL_BYTES;
            result.PipeConnection = true;

            return result;
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