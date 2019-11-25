namespace Twino.MQ.Clients
{
    /// <summary>
    /// Client information.
    /// Each client should send this information to server and each client can have only one-time defined information.
    /// </summary>
    public class ClientInformation
    {
        /// <summary>
        /// Client's unique id.
        /// If it's null or empty, server will create new unique id for the client.
        /// </summary>
        public string UniqueId { get; set; }
        
        /// <summary>
        /// Client name.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Client type.
        /// If different type of clients join your server, you can categorize them with this type value.
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Client auhentication token.
        /// Usually bearer token.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// When client is connected and send it's first information data.
        /// Some Subscriptions can be attached.
        /// This array contains auto subscriptions for the client.
        /// </summary>
        public ClientSubscriptionInfo[] Subscriptions { get; set; }
    }
}