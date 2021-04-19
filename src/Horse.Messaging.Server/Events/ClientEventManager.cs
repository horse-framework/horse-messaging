using Horse.Messaging.Protocol.Models.Events;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Events
{
    /// <summary>
    /// Client event manager.
    /// Manages client connected, disconnected events
    /// </summary>
    public class ClientEventManager : EventManager
    {
        /// <summary>
        /// Creates new client event manager
        /// </summary>
        public ClientEventManager(string eventName, HorseRider server)
            : base(server, eventName, null)
        {
        }

        /// <summary>
        /// Triggers client connected or disconnected events
        /// </summary>
        public void Trigger(MessagingClient client, string node = null)
        {
            base.Trigger(new ClientEvent
                         {
                             Id = client.UniqueId,
                             Name = client.Name,
                             Type = client.Type,
                             Node = node
                         });
        }
    }
}