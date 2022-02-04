using System;

namespace Horse.Messaging.Server.Queues
{
    internal class PutBackQueueMessage
    {
        public DateTime PutBackDate { get; }
        public QueueMessage Message { get; }

        public PutBackQueueMessage(QueueMessage message, DateTime putBackDate)
        {
            Message = message;
            PutBackDate = putBackDate;
        }
    }
}