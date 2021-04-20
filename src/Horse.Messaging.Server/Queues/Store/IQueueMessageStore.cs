namespace Horse.Messaging.Server.Queues.Store
{
    /// <summary>
    /// Queue message store implementation stores queue messages.
    /// </summary>
    public interface IQueueMessageStore
    {
        /// <summary>
        /// Returns count of all stored messages
        /// </summary>
        /// <returns></returns>
        int CountAll();

        /// <summary>
        /// Returns count of stored regular messages
        /// </summary>
        /// <returns></returns>
        int CountRegular();

        /// <summary>
        /// Returns count of high priority marked messages
        /// </summary>
        /// <returns></returns>
        int CountPriority();

        /// <summary>
        /// Puts a message into message store 
        /// </summary>
        void Put(QueueMessage message);

        /// <summary>
        /// Gets next message from store
        /// </summary>
        QueueMessage GetNext();

        /// <summary>
        /// Puts a message back into the message store
        /// </summary>
        /// <param name="asNextConsuming">If true, message is put at the beginning of the queue</param>
        void PutBack(bool asNextConsuming);

        /// <summary>
        /// Clears all messages from the queue
        /// </summary>
        void ClearAll();
    }
}