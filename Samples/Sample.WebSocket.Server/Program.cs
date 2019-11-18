using Twino.Server;
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
            Console.WriteLine("# " + message);
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
            {
                string msg = Console.ReadLine();
                ServerClient.Send(msg);
            }
        }
    }
}