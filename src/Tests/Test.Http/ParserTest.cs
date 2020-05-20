using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Protocols;
using Twino.Protocols.Http;
using Xunit;

namespace Test.Http
{
    public class ParserTest
    {
        private static byte[] CreateRequest(int headerLength, int contentLength)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("POST / HTTP/1.1\r\n");
            int header = 17;
            string cont = "Content-Length: " + contentLength + "\r\n";
            header += cont.Length;
            builder.Append(cont);
            builder.Append("E: ");
            header += 3;

            while (header < headerLength)
            {
                builder.Append("u");
                header++;
            }

            builder.Append("\r\n");

            int content = 12;
            builder.Append("\r\n");
            builder.Append("Form=1234&A=");
            while (content < contentLength)
            {
                content++;
                builder.Append("a");
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        [Fact]
        public async Task Read()
        {
            for (int i = 40; i < 700; i++)
            for (int j = 30; j < 2000; j++)
            {
                byte[] bytes = CreateRequest(i, j);

                byte[] protocol = new byte[8];
                Array.Copy(bytes, 0, protocol, 0, protocol.Length);

                HttpReader reader = new HttpReader(new HttpOptions());
                reader.HandshakeResult = new ProtocolHandshakeResult
                                         {
                                             ReadAfter = true,
                                             PreviouslyRead = protocol
                                         };
                MemoryStream ms = new MemoryStream();
                ms.Write(bytes, 8, bytes.Length - 8);

                ms.Position = 0;
                HttpMessage message = await reader.Read(ms);

                Assert.Equal(j, message.Request.ContentStream.Length);
                Assert.Equal(message.Request.ContentLength, message.Request.ContentStream.Length);

                string str = Encoding.UTF8.GetString(message.Request.ContentStream.ToArray());
                Assert.StartsWith("Form", str);
            }
        }
    }
}