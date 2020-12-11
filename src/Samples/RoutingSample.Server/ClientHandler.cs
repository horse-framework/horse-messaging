using System;
using System.Threading.Tasks;
using Horse.Mq;
using Horse.Mq.Clients;

namespace Sample.Server
{
    public class ClientHandler : IClientHandler
    {
        public Task Connected(HorseMq server, MqClient client)
        {
            Console.WriteLine("Client connected");
            return Task.CompletedTask;
        }

        public Task Disconnected(HorseMq server, MqClient client)
        {
            Console.WriteLine("Client dsconnected");
            return Task.CompletedTask;
        }
    }
}