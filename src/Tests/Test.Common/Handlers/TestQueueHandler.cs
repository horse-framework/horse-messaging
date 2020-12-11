using System.Threading.Tasks;
using Horse.Mq;
using Horse.Mq.Clients;
using Horse.Mq.Queues;

namespace Test.Common.Handlers
{
    public class TestQueueHandler : IQueueEventHandler
    {
        private readonly TestHorseMq _mq;

        public TestQueueHandler(TestHorseMq mq)
        {
            _mq = mq;
        }

        public Task OnCreated(HorseQueue queue)
        {
            _mq.OnQueueCreated++;
            return Task.CompletedTask;
        }

        public Task OnRemoved(HorseQueue queue)
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

        public Task OnStatusChanged(HorseQueue queue, QueueStatus @from, QueueStatus to)
        {
            _mq.OnQueueStatusChanged++;
            return Task.CompletedTask;
        }
    }
}