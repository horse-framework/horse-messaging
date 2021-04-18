namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Queue content type object
    /// </summary>
    public class QueueId
    {
        /// <summary>
        /// Content type value
        /// </summary>
        public ushort Value { get; }

        /// <summary>
        /// Creates new queue id reference type
        /// </summary>
        public QueueId(ushort value)
        {
            Value = value;
        }
    }
}