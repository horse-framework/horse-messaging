using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Protocols.Http
{
    public class TwinoHttpProtocol : ITwinoProtocol<HttpMessage>
    {
        public string Name => "HTTP/1.1";
        public byte[] PingMessage => null;
        public byte[] PongMessage => null;
        
        public Task<bool> Check(byte[] data)
        {
            throw new System.NotImplementedException();
        }

        public IProtocolMessageReader<HttpMessage> CreateReader()
        {
            throw new System.NotImplementedException();
        }

        public IProtocolMessageWriter<HttpMessage> CreateWriter()
        {
            throw new System.NotImplementedException();
        }
    }
}