using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Protocols.TMQ
{
    public class TwinoTmqProtocol : ITwinoProtocol<TmqMessage>
    {
        public string Name => "tmq";
        public byte[] PingMessage => PredefinedMessages.PING;
        public byte[] PongMessage => PredefinedMessages.PONG;

        public async Task<bool> Check(byte[] data)
        {
            if (data.Length < 6)
                return await Task.FromResult(false);

            return data[0] == PredefinedMessages.HELLO_BYTE;
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