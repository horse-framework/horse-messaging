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
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, Dictionary<string, string> properties)
        {
            return await Task.FromResult(new WsServerSocket(server, connection));
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, WebSocketMessage message)
        {
            string msg = Encoding.UTF8.GetString(message.Content.ToArray());
            Console.WriteLine("# " + msg);
            await Task.CompletedTask;
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }
    }

    class Program
    {
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
            server.BlockWhileRunning();

            StickyConnector<TwinoWebSocket, WebSocketMessage> connector = new StickyConnector<TwinoWebSocket, WebSocketMessage>(TimeSpan.FromSeconds(10));
        }
    }
}