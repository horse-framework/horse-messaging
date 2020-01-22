using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class StopQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }

        private readonly ChannelQueue _queue;

        public StopQueueState(ChannelQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(ChannelClient client, TmqMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        public Task<PushResult> Push(QueueMessage message, MqClient sender)
        {
            return Task.FromResult(PushResult.StatusNotSupported);
        }

        public Task Trigger()
        {
            return Task.CompletedTask;
        }

        public Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus)
        {
            lock (_queue.HighPriorityLinkedList)
                _queue.HighPriorityLinkedList.Clear();

            lock (_queue.RegularLinkedList)
                _queue.RegularLinkedList.Clear();

            _queue.TimeKeeper.Reset();

            return Task.FromResult(QueueStatusAction.Allow);
        }

        public Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus)
        {
            return Task.FromResult(QueueStatusAction.Allow);
        }
    }
}