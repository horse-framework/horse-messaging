using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;

namespace Test.Mq.Internal
{
    internal class TestChannelHandler : IChannelEventHandler
    {
        private readonly TestMqServer _server;

        internal TestChannelHandler(TestMqServer server)
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

        public async Task ClientJoined(ChannelClient client)
        {
            _server.ClientJoined++;
            await Task.CompletedTask;
        }

        public async Task ClientLeft(ChannelClient client)
        {
            _server.ClientLeft++;
            await Task.CompletedTask;
        }

        public async Task<bool> OnChannelStatusChanged(Channel channel, ChannelStatus @from, ChannelStatus to)
        {
            _server.OnChannelStatusChanged++;
            return await Task.FromResult(true);
        }

        public async Task<bool> OnQueueStatusChanged(ChannelQueue queue, QueueStatus @from, QueueStatus to)
        {
            _server.OnQueueStatusChanged++;
            return await Task.FromResult(true);
        }
    }
}