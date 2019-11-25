using System;
using Twino.MQ.Clients;

namespace Twino.MQ.Channels
{
    public class MessageDelivery
    {
        #region Properties

        public QueueMessage Message { get; }

        public QueueClient Receiver { get; }

        public bool IsSent { get; private set; }
        public DateTime SendDate { get; private set; }

        public bool IsDelivered { get; private set; }
        public DateTime DeliveryDate { get; private set; }

        public DateTime DeliveryDeadline { get; set; }

        #endregion

        #region Constructurs

        public MessageDelivery(QueueMessage message, QueueClient receiver)
            : this(message, receiver, DateTime.MinValue)
        {
            Message = message;
            Receiver = receiver;
        }

        public MessageDelivery(QueueMessage message, QueueClient receiver, DateTime deliveryDeadline)
        {
            Message = message;
            Receiver = receiver;
            DeliveryDeadline = deliveryDeadline;
        }

        #endregion

        #region Actions

        public void MarkAsSent()
        {
            IsSent = true;
            SendDate = DateTime.UtcNow;
        }

        public void MarkAsDelivered()
        {
            IsDelivered = true;
            DeliveryDate = DateTime.UtcNow;
        }

        #endregion
    }
}