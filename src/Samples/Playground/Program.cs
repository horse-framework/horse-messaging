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
        private static string _text =
            "POST /api/auth/login HTTP/1.1\r\n" +
            "Accept-Language: tr,en;q=0.9,en-GB;q=0.8,en-US;q=0.7\r\n"+
            "Accept-Encoding: gzip, deflate, br\r\n" +
            "Referer: http://localhost:4200/auth/login\r\n" +
            "Sec-Fetch-Mode: cors\r\n" +
            "Sec-Fetch-Dest: empty\r\n" +
            "Sec-Fetch-Site: same-origin\r\n" +
            "Origin: http://localhost:4200\r\n" +
            "Content-Type: application/x-www-form-urlencoded;charset=UTF-8\r\n" +
            "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.129 Safari/537.36 Edg/81.0.416.68\r\n" +
            "Accept: application/json, text/plain, */*\r\n" +
            "Content-Length: 64\r\n" +
            "Connection: close\r\n" +
            "Host: localhost:4200\r\n\r\n" +
            "form.EmailAddress=exxxxx@xxxxxxxxxx.com.xx&form.Password=123456";

        static async Task Main(string[] args)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(_text);
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
            ms.Write(bytes);

            ms.Position = 0;
            HttpMessage message = await reader.Read(ms);
            Console.WriteLine(message.Request.Headers.Count);
            Console.WriteLine(message.Request.Headers["Content-Length"]);
            
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