namespace Horse.Messaging.Server.Queues.Store
{
    public interface IQueueMessageStore
    {
        int CountAll();
        int CountRegular();
        int CountPriority();

        void Put(QueueMessage message);
        
        QueueMessage GetNext();

        void PutBack(bool asNextConsuming);

        void ClearAll();
    }
}