using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class CacheQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }

        private readonly ChannelQueue _queue;

        public CacheQueueState(ChannelQueue queue)
        {
            _queue = queue;
        }

        public async Task<PullResult> Pull(ChannelClient client, TmqMessage request)
        {
            throw new System.NotImplementedException();
        }

        public async Task<PushResult> Push(QueueMessage message, MqClient sender)
        {
            throw new System.NotImplementedException();
        }

        public Task Trigger()
        {
            return Task.CompletedTask;
        }
    }
}