using System.Collections.Generic;

namespace Horse.Messaging.Server.Queues.Store
{
    internal class LinkedMessageStore : IQueueMessageStore
    {
        private readonly HorseQueue _queue;
        private readonly LinkedList<QueueMessage> _messages = new LinkedList<QueueMessage>();
        private readonly LinkedList<QueueMessage> _messagesPrio = new LinkedList<QueueMessage>();

        public LinkedMessageStore(HorseQueue queue)
        {
            _queue = queue;
        }

        public int CountAll()
        {
            throw new System.NotImplementedException();
        }

        public int CountRegular()
        {
            throw new System.NotImplementedException();
        }

        public int CountPriority()
        {
            throw new System.NotImplementedException();
        }

        public void Put(QueueMessage message)
        {
            throw new System.NotImplementedException();
        }

        public QueueMessage GetNext()
        {
            throw new System.NotImplementedException();
        }

        public void PutBack(bool asNextConsuming)
        {
            throw new System.NotImplementedException();
        }

        public void ClearAll()
        {
            throw new System.NotImplementedException();
        }
    }
}