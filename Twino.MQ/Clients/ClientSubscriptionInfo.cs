namespace Twino.MQ.Clients
{
    public class ClientSubscriptionInfo
    {
        public string ChannelName { get; set; }
        public ushort[] ContentTypes { get; set; }
    }
}