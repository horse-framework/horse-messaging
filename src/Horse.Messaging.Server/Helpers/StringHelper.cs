using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Helpers
{
    internal static class StringHelper
    {
        public static QueueType ToQueueType(this string type)
        {
            switch (type.Trim().ToLowerInvariant())
            {
                case "push":
                    return QueueType.Push;

                case "pull":
                    return QueueType.Pull;

                case "rr":
                case "round":
                case "roundrobin":
                case "round-robin":
                    return QueueType.RoundRobin;

                default:
                    return QueueType.Push;
            }
        }
    }
}