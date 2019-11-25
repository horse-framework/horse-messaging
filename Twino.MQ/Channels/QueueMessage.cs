using System;
using System.Collections.Generic;
using System.Linq;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Channels
{
    /// <summary>
    /// Queue TMQ Message
    /// </summary>
    public class QueueMessage
    {
        /// <summary>
        /// Message creation date
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// TMQ Message
        /// </summary>
        public TmqMessage Message { get; set; }

        /// <summary>
        /// If true, message is saved to DB or Disk
        /// </summary>
        public bool IsSaved { get; set; }

        private readonly List<MessageDelivery> _deliveries = new List<MessageDelivery>();

        /// <summary>
        /// Message deliveries
        /// </summary>
        public IEnumerable<MessageDelivery> Deliveries => _deliveries;

        /// <summary>
        /// Creates new QueueMessage from TmqMessage with save status
        /// </summary>
        public QueueMessage(TmqMessage message, bool isSaved = false)
        {
            CreatedDate = DateTime.UtcNow;
            Message = message;
            IsSaved = isSaved;
        }

        /// <summary>
        /// True, if message is sent at least one client
        /// </summary>
        /// <returns></returns>
        public bool IsSent()
        {
            lock (_deliveries)
                return _deliveries.Any(x => x.IsSent);
        }

        /// <summary>
        /// True, if at last one delivery message is received.
        /// </summary>
        /// <returns></returns>
        public bool IsDelivered()
        {
            lock (_deliveries)
                return _deliveries.Any(x => x.IsDelivered);
        }
    }
}