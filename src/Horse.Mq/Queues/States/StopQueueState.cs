using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Queues.States
{
    internal class StopQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }
        public bool TriggerSupported => false;

        private readonly HorseQueue _queue;

        public StopQueueState(HorseQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(QueueClient client, HorseMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        public bool CanEnqueue(QueueMessage message)
        {
            return false;
        }

        public Task<PushResult> Push(QueueMessage message)
        {
            return Task.FromResult(PushResult.StatusNotSupported);
        }

        public Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus)
        {
            lock (_queue.PriorityMessagesList)
                _queue.PriorityMessagesList.Clear();

            lock (_queue.MessagesList)
                _queue.MessagesList.Clear();

            if (_queue.TimeKeeper != null)
                _queue.TimeKeeper.Reset();

            return Task.FromResult(QueueStatusAction.Allow);
        }

        public Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus)
        {
            return Task.FromResult(QueueStatusAction.Allow);
        }
    }
}