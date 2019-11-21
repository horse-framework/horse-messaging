namespace Twino.MQ.Channels
{
    public interface IChannelEventHandler
    {
        void OnJoin();

        void OnLeave();

        void OnQueueCreated();

        void OnQueueRemoved();

        void OnStatusChanged();
    }
}