using System;
using Twino.MQ.Clients;

namespace Twino.MQ.Channels
{
    public class MessageDelivery
    {
        public QueueMessage Message { get; set; }

        public QueueClient Receiver { get; set; }

        public bool IsSent { get; set; }
        public DateTime SendDate { get; set; }

        public bool IsDelivered { get; set; }
        public DateTime DeliveryDate { get; set; }
        
        public DateTime DeliveryDeadline { get; set; }
    }
}