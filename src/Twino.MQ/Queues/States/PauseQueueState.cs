using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class PauseQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }
        public bool TriggerSupported => false;

        private readonly TwinoQueue _queue;

        public PauseQueueState(TwinoQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(QueueClient client, TwinoMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        public bool CanEnqueue(QueueMessage message)
        {
            return true;
        }

        public Task<PushResult> Push(QueueMessage message)
        {
            return Task.FromResult(PushResult.Success);
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