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
        private static byte[] CreateSizeRequest(int headerLength, int contentLength)
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
            
            while (content < contentLength - 1)
            {
                content++;
                builder.Append("a");
            }

            builder.Append("b");

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        private static byte[] CreateCountRequest(int headerCount, int contentLength)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("POST / HTTP/1.1\r\n");
            builder.Append("Content-Length: " + contentLength + "\r\n");

            for (int i = 0; i < headerCount; i++)
                builder.Append("Header-" + i + ": " + "Value-" + i + "\r\n");

            builder.Append("\r\n");

            int content = 12;
            builder.Append("Form=1234&A=");
            while (content < contentLength - 1)
            {
                content++;
                builder.Append("a");
            }

            builder.Append("b");

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        [Fact]
        public async Task ReadHeaderCount()
        {
            for (int i = 2; i < 300; i++)
            for (int j = 30; j < 1000; j++)
            {
                byte[] bytes = CreateCountRequest(i, j);

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

                Assert.Equal(i, message.Request.Headers.Count - 1); // - 1 for "Content-Length" header
                Assert.Equal(j, message.Request.ContentStream.Length);
                Assert.Equal(message.Request.ContentLength, message.Request.ContentStream.Length);

                string str = Encoding.UTF8.GetString(message.Request.ContentStream.ToArray());
                Assert.StartsWith("Form", str);
                Assert.EndsWith("b", str);
            }
        }

        [Fact]
        public async Task ReadBySize()
        {
            for (int i = 40; i < 700; i++)
            for (int j = 30; j < 2000; j++)
            {
                byte[] bytes = CreateSizeRequest(i, j);

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
                Assert.EndsWith("b", str);
            }
        }
    }
}