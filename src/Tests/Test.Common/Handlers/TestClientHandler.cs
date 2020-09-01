using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;

namespace Test.Common.Handlers
{
    public class TestClientHandler : IClientHandler
    {
        private readonly TestTwinoMQ _mq;

        public TestClientHandler(TestTwinoMQ mq)
        {
            _mq = mq;
        }

        public Task Connected(TwinoMQ server, MqClient client)
        {
            Console.WriteLine("Client Connected");
            _mq.ClientConnected++;
            return Task.CompletedTask;
        }

        public Task Disconnected(TwinoMQ server, MqClient client)
        {
            Console.WriteLine("Client Disconnected");
            _mq.ClientDisconnected++;
            return Task.CompletedTask;
        }
    }
}