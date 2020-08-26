using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Test.Mq.Internal
{
    public class TestQueueHandler : IQueueEventHandler
    {
        private readonly TestMqServer _server;

        public TestQueueHandler(TestMqServer server)
        {
            _server = server;
        }

        public async Task OnQueueCreated(TwinoQueue queue, Channel channel)
        {
            _server.OnQueueCreated++;
            await Task.CompletedTask;
        }

        public async Task OnQueueRemoved(TwinoQueue queue, Channel channel)
        {
            _server.OnQueueRemoved++;
            await Task.CompletedTask;
        }

        public async Task OnClientJoined(QueueClient client)
        {
            _server.ClientJoined++;
            await Task.CompletedTask;
        }

        public async Task OnClientLeft(QueueClient client)
        {
            _server.ClientLeft++;
            await Task.CompletedTask;
        }

        public async Task OnQueueStatusChanged(TwinoQueue queue, QueueStatus @from, QueueStatus to)
        {
            _server.OnQueueStatusChanged++;
            await Task.CompletedTask;
        }

        public async Task OnChannelCreated(Channel channel)
        {
            await Task.CompletedTask;
        }

        public async Task OnChannelRemoved(Channel channel)
        {
            _server.OnChannelRemoved++;
            await Task.CompletedTask;
        }
    }
}