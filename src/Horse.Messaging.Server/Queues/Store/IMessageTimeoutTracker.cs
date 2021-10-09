namespace Horse.Messaging.Server.Queues.Store
{
    public interface IMessageTimeoutTracker
    {
        IQueueMessageStore Store { get; }

        void Start();

        void Stop();
    }
}