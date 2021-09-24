namespace Horse.Messaging.Server.Cluster
{
    public enum SyncMethod : byte
    {
        AddFirst = 0,
        AddAfter = 1,
        AddLast = 2,
        Completed = 3
    }

    public class MessageSyncMethod
    {
        public SyncMethod Method { get; }
        public string PreviousMessageId { get; }

        public MessageSyncMethod(SyncMethod method, string previousMessageId)
        {
            Method = method;
            PreviousMessageId = previousMessageId;
        }
    }
}