using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Helpers
{
    internal static class OptionsHelper
    {
        public static QueueAckDecision ToAckDecision(this string value)
        {
            switch (value.Trim().ToLower())
            {
                case "none":
                    return QueueAckDecision.None;

                case "request":
                    return QueueAckDecision.JustRequest;

                case "wait":
                    return QueueAckDecision.WaitForAcknowledge;

                default:
                    return QueueAckDecision.None;
            }
        }

        public static string FromAckDecision(this QueueAckDecision value)
        {
            switch (value)
            {
                case QueueAckDecision.None:
                    return "none";

                case QueueAckDecision.JustRequest:
                    return "request";

                case QueueAckDecision.WaitForAcknowledge:
                    return "wait";

                default:
                    return "none";
            }
        }

        public static MessageLimitExceededStrategy ToLimitExceededStrategy(this string value)
        {
            if (value == null)
                return MessageLimitExceededStrategy.RejectNewMessage;

            switch (value.Trim().ToLower())
            {
                case "reject":
                case "rejectnew":
                case "rejectnewmessage":
                    return MessageLimitExceededStrategy.RejectNewMessage;

                case "delete":
                case "deleteoldest":
                case "deleteoldestmessage":
                    return MessageLimitExceededStrategy.DeleteOldestMessage;

                default:
                    return MessageLimitExceededStrategy.RejectNewMessage;
            }
        }

        public static QueueDestroy ToQueueDestroy(this string value)
        {
            switch (value.Trim().ToLower())
            {
                case "disabled":
                    return QueueDestroy.Disabled;

                case "empty":
                    return QueueDestroy.Empty;

                case "no-consumer":
                    return QueueDestroy.NoConsumers;

                case "no-message":
                    return QueueDestroy.NoMessages;

                default:
                    return QueueDestroy.Disabled;
            }
        }

        public static string FromQueueDestroy(this QueueDestroy value)
        {
            switch (value)
            {
                case QueueDestroy.Disabled:
                    return "disabled";

                case QueueDestroy.Empty:
                    return "empty";

                case QueueDestroy.NoConsumers:
                    return "no-consumers";

                case QueueDestroy.NoMessages:
                    return "no-messages";

                default:
                    return "disabled";
            }
        }

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

        public static string FromQueueType(this QueueType value)
        {
            switch (value)
            {
                case QueueType.Pull:
                    return "pull";

                case QueueType.Push:
                    return "push";

                case QueueType.RoundRobin:
                    return "rr";

                default:
                    return "push";
            }
        }

        public static PutBackDecision ToPutBackDecision(this string value)
        {
            switch (value.Trim().ToLower())
            {
                case "no":
                    return PutBackDecision.No;

                case "regular":
                    return PutBackDecision.Regular;

                case "priority":
                    return PutBackDecision.Priority;

                default:
                    return PutBackDecision.No;
            }
        }

        public static CommitWhen ToCommitWhen(this string value)
        {
            switch (value.Trim().ToLower())
            {
                case "none":
                    return CommitWhen.None;

                case "afterreceived":
                    return CommitWhen.AfterReceived;

                case "aftersaved":
                    return CommitWhen.AfterSaved;

                case "aftersent":
                    return CommitWhen.AfterSent;

                case "afteracknowledge":
                    return CommitWhen.AfterAcknowledge;

                default:
                    return CommitWhen.None;
            }
        }
    }
}