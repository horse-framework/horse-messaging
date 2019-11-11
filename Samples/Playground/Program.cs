using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Server;
using Twino.Server.WebSockets;
using Twino.SocketModels;

namespace Playground
{
    public class CustomClient : ServerSocket
    {
        public CustomClient(TwinoServer server, HttpRequest request, TcpClient client) : base(server, request, client)
        {
        }

        protected override void OnConnected()
        {
            base.OnConnected();
            Console.WriteLine($"Client connected from {Request.IpAddress}");
        }

        public override void Disconnect()
        {
            base.Disconnect();
            Console.WriteLine("Client disconnected");
        }

        protected override void OnMessageReceived(string message)
        {
            Console.WriteLine($"Received: {message}");
            base.OnMessageReceived(message);
        }
    }

    public class WebSocketClientFactory : IClientFactory
    {
        public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
        {
            if (!request.Headers.ContainsKey(HttpHeaders.AUTHORIZATION))
                return await Task.FromResult((ServerSocket) null);

            CustomClient socket = new CustomClient(server, request, client);
            return await Task.FromResult(socket);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            ServerOptions options = ServerOptions.CreateDefault();
            options.Hosts[0].Port = 85; //listen port 85
            options.PingInterval = 120000; //120 seconds
            
            IClientFactory clientFactory = new WebSocketClientFactory();
            TwinoServer server = TwinoServer.CreateWebSocket(clientFactory, options);
            server.Start();
            server.BlockWhileRunning();
        }
    }
}