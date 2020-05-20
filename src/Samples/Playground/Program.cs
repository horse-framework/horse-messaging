using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Protocols;
using Twino.MQ.Data;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.Http;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;
using Twino.Server;

namespace Playground
{
    class Program
    {
        public static byte[] CreateRequest(int headerLength, int contentLength)
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

        static async Task Test(int i, int j, bool write)
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

            if (message.Request.ContentStream.Length != j)
                Console.WriteLine($"Length Failed: {i}, {j}");

            string str = Encoding.UTF8.GetString(message.Request.ContentStream.ToArray());
            if (write)
                Console.WriteLine(str);

            if (!str.StartsWith("Form"))
            {
                Console.WriteLine($"Form Failed: {i}, {j}");
            }
        }

        static async Task Main(string[] args)
        {

            for (int i = 40; i < 500; i++)
            for (int j = 30; j < 2000; j++)
                await Test(i, j, false);

            return;

            TwinoServer _server = new TwinoServer();
            _server = new TwinoServer(ServerOptions.CreateDefault());
            _server.UseWebSockets(async (socket) => { await socket.SendAsync("Welcome"); },
                                  async (socket, message) =>
                                  {
                                      Console.WriteLine("# " + message);
                                      await socket.SendAsync(message);
                                  });
            _server.Start(46100);
            _server.BlockWhileRunning();
        }
    }
}