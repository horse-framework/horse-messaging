namespace Twino.MQ.Queues
{
    /// <summary>
    /// Queue content type object
    /// </summary>
    public class QueueContentType
    {
        /// <summary>
        /// Content type value
        /// </summary>
        public ushort Value { get; }

        public QueueContentType(ushort value)
        {
            Value = value;
        }
    }
}