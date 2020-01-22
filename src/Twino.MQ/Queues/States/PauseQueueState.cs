using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class PauseQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }

        private readonly ChannelQueue _queue;

        public PauseQueueState(ChannelQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(ChannelClient client, TmqMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        public Task<PushResult> Push(QueueMessage message, MqClient sender)
        {
            _queue.AddMessage(message);
            return Task.FromResult(PushResult.Success);
        }

        public Task Trigger()
        {
            return Task.CompletedTask;
        }

        public Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus)
        {
            return Task.FromResult(QueueStatusAction.Allow);
        }

        public Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus)
        {
            return Task.FromResult(QueueStatusAction.Allow);
        }
    }
}