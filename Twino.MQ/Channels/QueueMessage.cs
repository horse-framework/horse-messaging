using System;
using System.Collections.Generic;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Channels
{
    public class QueueMessage
    {
        public DateTime CreatedDate { get; set; }
        public TmqMessage Message { get; set; }

        public bool IsSaved { get; set; }

        private readonly List<MessageDelivery> _deliveries = new List<MessageDelivery>();
        public IEnumerable<MessageDelivery> Deliveries => _deliveries;

        public QueueMessage(TmqMessage message, bool isSaved = false)
        {
            CreatedDate = DateTime.UtcNow;
            Message = message;
            IsSaved = isSaved;
        }
    }
}