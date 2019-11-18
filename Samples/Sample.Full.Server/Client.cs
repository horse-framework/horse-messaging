using Twino.Server;
using System;
using System.Net.Sockets;
using Twino.Core.Http;

namespace Sample.Full.Server
{
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
            Console.WriteLine("Connected");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine("Disconnected");
        }

        protected override void OnMessageReceived(string message)
        {
            Console.WriteLine("Received: " + message);
        }

    }
}
