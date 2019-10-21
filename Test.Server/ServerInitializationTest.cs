using System.Net;
using System.Threading.Tasks;
using Twino.Server;
using Twino.Server.Http;
using Twino.Server.WebSockets;
using Test.Server.Helpers;
using Twino.Client;
using Xunit;

namespace Test.Server
{
    public class ServerInitializationTest
    {
        [Fact]
        public void HttpHandlerClass()
        {
            IHttpRequestHandler handler = new HttpHandler();
            ServerOptions options = ServerOptions.CreateDefault();
            options.ContentEncoding = null;

            TwinoServer server = TwinoServer.CreateHttp(handler, options);
            server.Start(381);

            System.Threading.Thread.Sleep(250);

            WebClient wc = new WebClient();
            string data = wc.DownloadString("http://127.0.0.1:381");
            Assert.Equal("{\"Success\":true,\"Message\":\"Hello world\"}", data);
        }

        [Fact]
        public void HttpHandlerDelegate()
        {
            ServerOptions options = ServerOptions.CreateDefault();
            options.ContentEncoding = null;

            TwinoServer server = TwinoServer.CreateHttp((twinoServer, request, response) => { response.SetToJson(new {Success = true, Message = "Hello world"}); }, options);
            server.Start(382);

            System.Threading.Thread.Sleep(250);

            WebClient wc = new WebClient();
            string data = wc.DownloadString("http://127.0.0.1:382");
            Assert.Equal("{\"Success\":true,\"Message\":\"Hello world\"}", data);
        }

        [Fact]
        public void WebSocketHandlerClass()
        {
            IClientFactory factory = new WebSocketClientFactory();
            TwinoServer server = TwinoServer.CreateWebSocket(factory);
            server.Start(383);

            System.Threading.Thread.Sleep(250);
            Connect(383);
        }

        [Fact]
        public void WebSocketHandlerDelegate()
        {
            TwinoServer server = TwinoServer.CreateWebSocket(async (twinoServer, request, client) =>
            {
                ServerSocket socket = new ServerSocket(twinoServer, request, client);
                socket.MessageReceived += (s, m) => { s.Send("Message from server"); };
                return await Task.FromResult(socket);
            });
            
            server.Start(384);

            System.Threading.Thread.Sleep(250);
            Connect(384);
        }

        [Fact]
        public void WebSocketAction()
        {
            TwinoServer server = TwinoServer.CreateWebSocket(socket =>
            {
                socket.MessageReceived += (s, m) => { s.Send("Message from server"); };
            });
            server.Start(385);

            System.Threading.Thread.Sleep(250);
            Connect(385);
        }

        private static void Connect(int port)
        {
            bool received = false;
            TwinoClient client = new TwinoClient();
            client.Connect("127.0.0.1", port, false);
            client.MessageReceived += (s, message) => received = true;
            client.Send("Hello");

            System.Threading.Thread.Sleep(250);
            Assert.True(client.IsConnected);
            Assert.True(received);
        }
        
    }
}