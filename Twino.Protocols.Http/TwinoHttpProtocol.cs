using System;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Protocols.Http
{
    public class TwinoHttpProtocol : ITwinoProtocol<HttpMessage>
    {
        public string Name => "HTTP/1.1";
        public byte[] PingMessage => null;
        public byte[] PongMessage => null;

        private static readonly byte[][] PROTOCOL_CHECK_LIST =
        {
            Encoding.ASCII.GetBytes("GET "),
            Encoding.ASCII.GetBytes("POST "),
            Encoding.ASCII.GetBytes("PUT "),
            Encoding.ASCII.GetBytes("PATCH "),
            Encoding.ASCII.GetBytes("OPTION"),
            Encoding.ASCII.GetBytes("HEAD "),
            Encoding.ASCII.GetBytes("DELETE"),
            Encoding.ASCII.GetBytes("TRACE "),
            Encoding.ASCII.GetBytes("CONNEC"),
        };

        public HttpOptions Options { get; set; }

        public TwinoHttpProtocol(HttpOptions options)
        {
            Options = options;
        }

        public async Task<ProtocolHandshakeResult> Check(byte[] data)
        {
            ProtocolHandshakeResult result = new ProtocolHandshakeResult();
            result.Accepted = CheckSync(data);
            if (result.Accepted)
                result.ReadAfter = true;
            
            return await Task.FromResult(result);
        }

        private static bool CheckSync(byte[] data)
        {
            Span<byte> span = data;
            foreach (byte[] arr in PROTOCOL_CHECK_LIST)
            {
                if (span.StartsWith(arr))
                    return true;
            }

            return false;
        }

        public IProtocolMessageReader<HttpMessage> CreateReader()
        {
            return new HttpReader(Options);
        }

        public IProtocolMessageWriter<HttpMessage> CreateWriter()
        {
            return new HttpWriter(Options);
        }
    }
}