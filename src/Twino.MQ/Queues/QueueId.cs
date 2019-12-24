namespace Twino.MQ.Queues
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

        public QueueId(ushort value)
        {
            Value = value;
        }
    }
}