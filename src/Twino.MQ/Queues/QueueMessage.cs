using System;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues
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
        /// The deadline that message expires
        /// </summary>
        public DateTime? Deadline { get; internal set; }

        /// <summary>
        /// TMQ Message
        /// </summary>
        public TmqMessage Message { get; set; }

        /// <summary>
        /// Real source client of the message
        /// </summary>
        public MqClient Source { get; set; }

        /// <summary>
        /// If true, message is saved to DB or Disk
        /// </summary>
        public bool IsSaved { get; internal set; }

        /// <summary>
        /// True, if message sending skipped by user
        /// </summary>
        public bool IsSkipped { get; internal set; }

        /// <summary>
        /// True, if acknowledge message for the message is received
        /// </summary>
        public bool IsAcknowledged { get; internal set; }

        /// <summary>
        /// True, if is sent
        /// </summary>
        public bool IsSent { get; internal set; }

        /// <summary>
        /// How many times sent, usually that count equals to receivers count
        /// </summary>
        public int SendCount { get; private set; }

        /// <summary>
        /// If in pending duration, there is no available receivers, this value will be true.
        /// That means, message isn't sent to anyone.
        /// </summary>
        public bool IsTimedOut { get; internal set; }

        /// <summary>
        /// True, if message is first in queue.
        /// If first operation skipped, or remove is skipped, message will be still in the queue but this value will be false. 
        /// </summary>
        public bool IsFirstQueue { get; set; }
        
        /// <summary>
        /// Last decision for the message
        /// </summary>
        public Decision Decision { get; set; }

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
        /// Sets message as sent
        /// </summary>
        public void MarkAsSent()
        {
            if (Message.FirstAcquirer)
                Message.FirstAcquirer = false;

            if (!IsSent)
                IsSent = true;

            SendCount++;
        }
    }
}