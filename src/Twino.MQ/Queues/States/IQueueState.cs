using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal interface IQueueState
    {
        QueueMessage ProcessingMessage { get; }

        bool TriggerSupported { get; }

        bool CanEnqueue(QueueMessage message);

        Task<PushResult> Push(QueueMessage message);

        Task<PullResult> Pull(QueueClient client, TwinoMessage request);

        Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus);

        Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus);
    }
}