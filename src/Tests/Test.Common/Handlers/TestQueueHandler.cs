using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Test.Common.Handlers
{
    public class TestQueueHandler : IQueueEventHandler
    {
        private readonly TestTwinoMQ _mq;

        public TestQueueHandler(TestTwinoMQ mq)
        {
            _mq = mq;
        }

        public Task OnCreated(TwinoQueue queue)
        {
            _mq.OnQueueCreated++;
            return Task.CompletedTask;
        }

        public Task OnRemoved(TwinoQueue queue)
        {
            _mq.OnQueueRemoved++;
            return Task.CompletedTask;
        }

        public Task OnConsumerSubscribed(QueueClient client)
        {
            _mq.OnSubscribed++;
            return Task.CompletedTask;
        }

        public Task OnConsumerUnsubscribed(QueueClient client)
        {
            _mq.OnUnsubscribed++;
            return Task.CompletedTask;
        }

        public Task OnStatusChanged(TwinoQueue queue, QueueStatus @from, QueueStatus to)
        {
            _mq.OnQueueStatusChanged++;
            return Task.CompletedTask;
        }
    }
}