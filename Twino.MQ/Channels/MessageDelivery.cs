using System;
using Twino.MQ.Clients;

namespace Twino.MQ.Channels
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
        /// If true, delivery is received (or response)
        /// </summary>
        public bool IsDelivered { get; private set; }

        /// <summary>
        /// Delivery receive time
        /// </summary>
        public DateTime DeliveryDate { get; private set; }

        /// <summary>
        /// Delivery wait deadline.
        /// If this value is set and delivery isn't received, time up methods will be called.
        /// </summary>
        public DateTime? DeliveryDeadline { get; }

        #endregion

        #region Constructurs

        /// <summary>
        /// Creates new message without delivery deadline
        /// </summary>
        internal MessageDelivery(QueueMessage message, ChannelClient receiver)
            : this(message, receiver, DateTime.MinValue)
        {
            Message = message;
            Receiver = receiver;
        }

        /// <summary>
        /// Creates new message with delivery deadline
        /// </summary>
        internal MessageDelivery(QueueMessage message, ChannelClient receiver, DateTime? deliveryDeadline)
        {
            Message = message;
            Receiver = receiver;
            DeliveryDeadline = deliveryDeadline;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Marks message as sent
        /// </summary>
        public void MarkAsSent()
        {
            IsSent = true;
            SendDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks message as delivered
        /// </summary>
        public void MarkAsDelivered()
        {
            IsDelivered = true;
            DeliveryDate = DateTime.UtcNow;
        }

        #endregion
    }
}