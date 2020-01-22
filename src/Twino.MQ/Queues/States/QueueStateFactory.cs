namespace Twino.MQ.Queues.States
{
    internal class QueueStateFactory
    {
        internal static IQueueState Create(ChannelQueue queue, QueueStatus status)
        {
            switch (status)
            {
                case QueueStatus.Route:
                    return new RouteQueueState(queue);

                case QueueStatus.Push:
                    return new PushQueueState(queue);

                case QueueStatus.RoundRobin:
                    return new RoundRobinQueueState(queue);

                case QueueStatus.Pull:
                    return new PullQueueState(queue);

                case QueueStatus.Cache:
                    return new CacheQueueState(queue);

                case QueueStatus.Paused:
                    return new PauseQueueState(queue);

                case QueueStatus.Stopped:
                    return new StopQueueState(queue);

                default:
                    return new StopQueueState(queue);
            }
        }
    }
}