namespace Twino.Client.TMQ.Models
{
    /// <summary>
    /// Consuming order
    /// </summary>
    public enum MessageOrder
    {
        /// <summary>
        /// Does not send any order information. Uses default 
        /// </summary>
        Default = 0,

        /// <summary>
        /// Requests messages with first in first out order (as queue)
        /// </summary>
        FIFO = 1,

        /// <summary>
        /// Requests messages with last in first out order (as stack)
        /// </summary>
        LIFO = 2
    }

    /// <summary>
    /// Pull request form
    /// </summary>
    public class PullRequest
    {
        /// <summary>
        /// Channel name
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Queue Id
        /// </summary>
        public ushort QueueId { get; set; }

        /// <summary>
        /// Maximum message count wanted received 
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// After all messages are received, clearing left messages in queue option
        /// </summary>
        public ClearDecision ClearAfter { get; set; }

        /// <summary>
        /// If true each message will have two headers
        /// "Priority-Messages" and "Messages" includes left messages in queue
        /// </summary>
        public bool GetQueueMessageCounts { get; set; }

        /// <summary>
        /// Consuming order
        /// </summary>
        public MessageOrder Order { get; set; }
    }
}