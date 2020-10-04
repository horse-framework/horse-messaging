using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;

namespace Sample.Server
{
    public class ClientHandler : IClientHandler
    {
        public Task Connected(TwinoMQ server, MqClient client)
        {
            Console.WriteLine("Client connected");
            return Task.CompletedTask;
        }

        public Task Disconnected(TwinoMQ server, MqClient client)
        {
            Console.WriteLine("Client dsconnected");
            return Task.CompletedTask;
        }
    }
}