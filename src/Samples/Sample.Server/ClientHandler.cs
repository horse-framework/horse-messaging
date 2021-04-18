using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace Sample.Server
{
    public class ClientHandler : IClientHandler
    {
        public Task Connected(HorseMq server, MessagingClient client)
        {
            Console.WriteLine("Client connected");
            return Task.CompletedTask;
        }

        public Task Disconnected(HorseMq server, MessagingClient client)
        {
            Console.WriteLine("Client dsconnected");
            return Task.CompletedTask;
        }
    }
}