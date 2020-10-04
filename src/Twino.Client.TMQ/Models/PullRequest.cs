using System.Collections.Generic;

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
        /// Queue name
        /// </summary>
        public string Queue { get; set; }

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

        /// <summary>
        /// Additinal headers for pull request message
        /// </summary>
        public List<KeyValuePair<string, string>> RequestHeaders { get; }

        /// <summary>
        /// Creates new pull request
        /// </summary>
        public PullRequest()
        {
            RequestHeaders = new List<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Creates request for pulling single message with default options
        /// </summary>
        public static PullRequest Single(string queue)
        {
            return new PullRequest
                   {
                       Queue = queue,
                       Count = 1,
                       Order = MessageOrder.Default,
                       ClearAfter = ClearDecision.None,
                       GetQueueMessageCounts = false
                   };
        }
    }
}