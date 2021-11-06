using System;
using System.Collections.Generic;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Queue Horse Message
    /// </summary>
    public class QueueMessage
    {
        /// <summary>
        /// Message creation date
        /// </summary>
        public DateTime CreatedDate { get; }

        /// <summary>
        /// The deadline that message expires
        /// </summary>
        public DateTime? Deadline { get; internal set; }

        /// <summary>
        /// Horse Message
        /// </summary>
        public HorseMessage Message { get; set; }

        /// <summary>
        /// Real source client of the message
        /// </summary>
        public MessagingClient Source { get; set; }

        /// <summary>
        /// If true, message is saved to DB or Disk
        /// </summary>
        public bool IsSaved { get; internal set; }

        /// <summary>
        /// True, if producer received ack
        /// </summary>
        public bool IsProducerAckSent { get; internal set; }

        /// <summary>
        /// True, if is sent
        /// </summary>
        public bool IsSent { get; internal set; }

        /// <summary>
        /// How many times sent, usually that count equals to receivers count
        /// </summary>
        public int SendCount { get; private set; }

        /// <summary>
        /// Last decision for the message
        /// </summary>
        public Decision Decision { get; set; }

        /// <summary>
        /// If true, message is in queue
        /// </summary>
        internal bool IsInQueue { get; set; }

        /// <summary>
        /// If true, message is timed out
        /// </summary>
        internal bool IsTimedOut { get; private set; }

        /// <summary>
        /// If true, message is completely removed
        /// </summary>
        internal bool IsRemoved { get; private set; }

        /// <summary>
        /// Payload object for end-user usage
        /// </summary>
        public object Payload { get; set; }

        /// <summary>
        /// Delivery count for the message.
        /// The value tells how many times message is proceed to send (multiple consumers are counted 1)
        /// </summary>
        public int DeliveryCount { get; internal set; }

        /// <summary>
        /// All receivers for current delivery.
        /// That list is reset before each delivery (if message ack timed out or nack received etc)
        /// </summary>
        internal List<QueueClient> CurrentDeliveryReceivers { get; } = new List<QueueClient>();

        /// <summary>
        /// Creates new QueueMessage from HorseMessage with save status
        /// </summary>
        public QueueMessage(HorseMessage message, bool isSaved = false)
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
            if (!IsSent)
                IsSent = true;

            SendCount++;
        }

        /// <summary>
        /// Sets message as removed
        /// </summary>
        public void MarkAsRemoved()
        {
            IsRemoved = true;
        }

        /// <summary>
        /// Sets message as timed out
        /// </summary>
        public void MarkAsTimedOut()
        {
            IsTimedOut = true;
        }
    }
}