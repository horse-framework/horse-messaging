using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace ClusteringSample.Server
{
    public class ClientEventHandler : IClientHandler
    {
        public Task Connected(HorseRider server, MessagingClient client)
        {
            Console.WriteLine("Client Connected");
            return Task.CompletedTask;
        }

        public Task Disconnected(HorseRider server, MessagingClient client)
        {
            Console.WriteLine("Client Disconnected");
            return Task.CompletedTask;
        }
    }
}