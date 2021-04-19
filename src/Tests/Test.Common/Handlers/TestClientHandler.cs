using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace Test.Common.Handlers
{
    public class TestClientHandler : IClientHandler
    {
        private readonly TestHorseMq _mq;

        public TestClientHandler(TestHorseMq mq)
        {
            _mq = mq;
        }

        public Task Connected(HorseRider server, MessagingClient client)
        {
            Console.WriteLine("Client Connected");
            _mq.ClientConnected++;
            return Task.CompletedTask;
        }

        public Task Disconnected(HorseRider server, MessagingClient client)
        {
            Console.WriteLine("Client Disconnected");
            _mq.ClientDisconnected++;
            return Task.CompletedTask;
        }
    }
}