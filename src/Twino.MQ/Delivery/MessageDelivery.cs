using System;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Twino.MQ.Delivery
{
    /// <summary>
    /// Message delivery data for a single message to a single receiver
    /// </summary>
    public class MessageDelivery
    {
        #region Properties

        /// <summary>
        /// True, if receiver is the first acquirer of the message
        /// </summary>
        public bool FirstAcquirer { get; internal set; }

        /// <summary>
        /// The message
        /// </summary>
        public QueueMessage Message { get; }

        /// <summary>
        /// Message receiver client
        /// </summary>
        public ChannelClient Receiver { get; }

        /// <summary>
        /// If true, message is sent
        /// </summary>
        public bool IsSent { get; private set; }

        /// <summary>
        /// Message send date
        /// </summary>
        public DateTime SendDate { get; private set; }

        /// <summary>
        /// If true, acknowledge is received
        /// </summary>
        public bool IsAcknowledged { get; private set; }

        /// <summary>
        /// If true, receivers does not send acknowledge message in acknowledge pending duration.
        /// </summary>
        public bool IsAcknowledgeTimedUp { get; private set; }

        /// <summary>
        /// Acknowledge receive time
        /// </summary>
        public DateTime AcknowledgeDate { get; private set; }

        /// <summary>
        /// Acknowledge wait deadline.
        /// If this value is set and acknowledge isn't received, time up methods will be called.
        /// </summary>
        public DateTime? AcknowledgeDeadline { get; }

        /// <summary>
        /// True, if acknowledge message is sent to message source
        /// </summary>
        public bool AcknowledgeSentToSource { get; internal set; }

        #endregion

        #region Constructurs

        /// <summary>
        /// Creates new message without acknowledge deadline
        /// </summary>
        internal MessageDelivery(QueueMessage message, ChannelClient receiver)
            : this(message, receiver, DateTime.MinValue)
        {
            Message = message;
            Receiver = receiver;
        }

        /// <summary>
        /// Creates new message with acknowledge deadline
        /// </summary>
        internal MessageDelivery(QueueMessage message, ChannelClient receiver, DateTime? acknowledgeDeadline)
        {
            Message = message;
            Receiver = receiver;
            AcknowledgeDeadline = acknowledgeDeadline;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Marks message as sent
        /// </summary>
        public void MarkAsSent()
        {
            Message.MarkAsSent();

            IsSent = true;
            SendDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks message as acknowledged
        /// </summary>
        public void MarkAsAcknowledged()
        {
            IsAcknowledged = true;
            AcknowledgeDate = DateTime.UtcNow;
            Message.IsAcknowledged = true;
        }

        /// <summary>
        /// Marks acknowledge is timed up
        /// </summary>
        public void MarkAsAcknowledgeTimedUp()
        {
            IsAcknowledged = false;
            IsAcknowledgeTimedUp = true;
        }

        #endregion
    }
}