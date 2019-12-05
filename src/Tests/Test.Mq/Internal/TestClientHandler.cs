using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;

namespace Test.Mq.Internal
{
    internal class TestClientHandler : IClientHandler
    {
        private readonly TestMqServer _server;

        internal TestClientHandler(TestMqServer server)
        {
            _server = server;
        }

        public async Task Connected(MqServer server, MqClient client)
        {
            Console.WriteLine("Client Connected");
            _server.ClientConnected++;
            await Task.CompletedTask;
        }

        public async Task Disconnected(MqServer server, MqClient client)
        {
            Console.WriteLine("Client Disconnected");
            _server.ClientDisconnected++;
            await Task.CompletedTask;
        }
    }
}