using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace Test.Common.Handlers
{
    public class TestQueueHandler : IQueueEventHandler
    {
        private readonly TestHorseRider _rider;

        public TestQueueHandler(TestHorseRider rider)
        {
            _rider = rider;
        }

        public Task OnCreated(HorseQueue queue)
        {
            _rider.OnQueueCreated++;
            return Task.CompletedTask;
        }

        public Task OnRemoved(HorseQueue queue)
        {
            _rider.OnQueueRemoved++;
            return Task.CompletedTask;
        }

        public Task OnConsumerSubscribed(QueueClient client)
        {
            _rider.OnSubscribed++;
            return Task.CompletedTask;
        }

        public Task OnConsumerUnsubscribed(QueueClient client)
        {
            _rider.OnUnsubscribed++;
            return Task.CompletedTask;
        }

        public Task OnStatusChanged(HorseQueue queue, QueueStatus @from, QueueStatus to)
        {
            _rider.OnQueueStatusChanged++;
            return Task.CompletedTask;
        }
    }
}