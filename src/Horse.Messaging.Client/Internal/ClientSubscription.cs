namespace Horse.Messaging.Client.Internal
{
    internal class ClientSubscription
    {
        public ClientSubscriptionType Type { get; set; }
        public string Target { get; set; }
    }
}