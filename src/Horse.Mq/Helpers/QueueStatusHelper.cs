using Horse.Mq.Queues;

namespace Horse.Mq.Helpers
{
    internal static class QueueStatusHelper
    {
        internal static QueueStatus FindStatus(string statusName)
        {
            switch (statusName.Trim().ToLower())
            {
                case "broadcast":
                    return QueueStatus.Broadcast;

                case "push":
                    return QueueStatus.Push;

                case "round":
                case "roundrobin":
                case "round-robin":
                    return QueueStatus.RoundRobin;

                case "pull":
                    return QueueStatus.Pull;

                case "cache":
                    return QueueStatus.Cache;

                case "pause":
                case "paused":
                    return QueueStatus.Paused;

                case "stop":
                case "stoped":
                case "stopped":
                    return QueueStatus.Stopped;

                default:
                    return QueueStatus.Broadcast;
            }
        }
    }
}