using Twino.Server;
using Twino.Server.WebSockets;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Sample.WebSocket.Server
{
    /// <summary>
    /// Server-Side WebSocket Client
    /// </summary>
    public class Client : ServerSocket
    {
        public Client(TwinoServer server, HttpRequest request, TcpClient client) : base(server, request, client)
        {
        }

        protected override void OnBinaryReceived(byte[] payload)
        {
        }

        protected override void OnConnected()
        {
            Console.WriteLine("Client Connected");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine("Client Disconnected");
        }

        protected override void OnMessageReceived(string message)
        {
            System.Threading.Thread.Sleep(1250);
            if (message == ".")
                Send(".");
        }
    }

    /// <summary>
    /// WebSocket Client Factory
    /// </summary>
    public class ClientFactory : IClientFactory
    {
        private Random rnd = new Random();

        public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
        {
            Client c = new Client(server, request, client);
            Program.ServerClient = c;
            return await Task.FromResult(c);
        }
    }

    class Program
    {
        public static ServerSocket ServerClient { get; set; }

        static void Main(string[] args)
        {
            IClientFactory factory = new ClientFactory();
            TwinoServer server = TwinoServer.CreateWebSocket(factory);
            server.Start();

            while (true)
                Console.ReadLine();
        }
    }
}