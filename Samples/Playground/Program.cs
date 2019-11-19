using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Client.WebSocket;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.Http;
using Twino.Protocols.WebSocket;
using Twino.Server;
using Twino.SocketModels;

namespace Playground
{
    [Route("")]
    public class HomeController : TwinoController
    {
        [HttpGet("")]
        public async Task<IActionResult> Get()
        {
            return await StringAsync("Hello, world!");
        }
    }

    public class WebSocketHandler : IProtocolConnectionHandler<WebSocketMessage>
    {
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            Console.WriteLine("> Connected");
            WsServerSocket socket = new WsServerSocket(server, connection);
            Program.Socket = socket;
            return await Task.FromResult(socket);
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, WebSocketMessage message)
        {
            string msg = Encoding.UTF8.GetString(message.Content.ToArray());
            Console.WriteLine("# " + msg);
            await Task.CompletedTask;
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            Console.WriteLine("> Disconnected");
            await Task.CompletedTask;
        }
    }

    class Program
    {
        public static WsServerSocket Socket { get; set; }
        
        static void Main(string[] args)
        {
            HttpOptions options = new HttpOptions();
            options.SupportedEncodings = new ContentEncodings[0];
            TwinoMvc mvc = new TwinoMvc();
            mvc.Init(m => { });

            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
            server.UseMvc(mvc, options);
            server.UseWebSockets(new WebSocketHandler());

            server.Start(82);

            while (true)
            {
                string line = Console.ReadLine();
                if (Socket != null)
                    Socket.Send(line);
            }
        }
    }
}