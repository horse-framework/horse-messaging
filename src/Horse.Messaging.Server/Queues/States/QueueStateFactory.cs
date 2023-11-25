namespace Horse.Messaging.Server.Queues.States;

internal class QueueStateFactory
{
    internal static IQueueState Create(HorseQueue queue, QueueType type)
    {
        switch (type)
        {
            case QueueType.Push:
                return new PushQueueState(queue);

            case QueueType.RoundRobin:
                return new RoundRobinQueueState(queue);

            case QueueType.Pull:
                return new PullQueueState(queue);

            default:
                return null;
        }
    }
}