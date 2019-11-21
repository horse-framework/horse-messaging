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
        
        public IEnumerable<MessageDelivery> Deliveries { get; set; }
    }
}