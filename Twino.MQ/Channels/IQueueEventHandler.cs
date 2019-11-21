namespace Twino.MQ.Channels
{
    public interface IQueueEventHandler
    {
        void OnSubscription();

        void OnUnsubscription();

        void OnMessageAdded();

        void OnMessageRemoved();
        
        void OnStatusChanged();
    }
}