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
        public bool IsSaved { get; internal set; }
        
        /// <summary>
        /// True, if message sending skipped by user
        /// </summary>
        public bool IsSkipped { get; internal set; }
        
        /// <summary>
        /// True, if message is first in queue.
        /// If first operation skipped, or remove is skipped, message will be still in the queue but this value will be false. 
        /// </summary>
        public bool IsFirstQueue { get; set; }

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

        /// <summary>
        /// Sets message as sent and adds delivery to message deliveries
        /// </summary>
        public void AddSend(MessageDelivery delivery)
        {
            if (Message.FirstAcquirer)
                Message.FirstAcquirer = false;

            delivery.MarkAsSent();
            
            lock (_deliveries)
                _deliveries.Add(delivery);
        }

        /// <summary>
        /// Sets message as delivered and updates deliveries
        /// </summary>
        public void AddDelivery()
        {
            throw new NotImplementedException();
        }
    }
}