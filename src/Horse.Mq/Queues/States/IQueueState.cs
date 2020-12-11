using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Queues.States
{
    internal interface IQueueState
    {
        QueueMessage ProcessingMessage { get; }

        bool TriggerSupported { get; }

        bool CanEnqueue(QueueMessage message);

        Task<PushResult> Push(QueueMessage message);

        Task<PullResult> Pull(QueueClient client, HorseMessage request);

        Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus);

        Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus);
    }
}