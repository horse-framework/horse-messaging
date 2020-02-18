using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal interface IQueueState
    {
        QueueMessage ProcessingMessage { get; }

        QueueMessage EnqueueDequeue(QueueMessage message);
        
        Task<PushResult> Push(QueueMessage message, MqClient sender);

        Task<PullResult> Pull(ChannelClient client, TmqMessage request);

        Task Trigger();

        Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus);
        
        Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus);
    }
}