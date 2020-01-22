using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Test.Mq.Internal
{
    public class TestChannelHandler : IChannelEventHandler
    {
        private readonly TestMqServer _server;

        public TestChannelHandler(TestMqServer server)
        {
            _server = server;
        }

        public async Task OnQueueCreated(ChannelQueue queue, Channel channel)
        {
            _server.OnQueueCreated++;
            await Task.CompletedTask;
        }

        public async Task OnQueueRemoved(ChannelQueue queue, Channel channel)
        {
            _server.OnQueueRemoved++;
            await Task.CompletedTask;
        }

        public async Task OnClientJoined(ChannelClient client)
        {
            _server.ClientJoined++;
            await Task.CompletedTask;
        }

        public async Task OnClientLeft(ChannelClient client)
        {
            _server.ClientLeft++;
            await Task.CompletedTask;
        }

        public async Task OnQueueStatusChanged(ChannelQueue queue, QueueStatus @from, QueueStatus to)
        {
            _server.OnQueueStatusChanged++;
            await Task.CompletedTask;
        }

        public async Task OnChannelRemoved(Channel channel)
        {
            _server.OnChannelRemoved++;
            await Task.CompletedTask;
        }
    }
}