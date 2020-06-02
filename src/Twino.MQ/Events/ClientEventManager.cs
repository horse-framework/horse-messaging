using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.MQ.Events
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
        public ClientEventManager(string eventName, MqServer server)
            : base(eventName, null, 0)
        {
        }

        /// <summary>
        /// Triggers client connected or disconnected events
        /// </summary>
        public Task Trigger(MqClient client, string node = null)
        {
            return base.Trigger(new ClientEvent
                                {
                                    Id = client.UniqueId,
                                    Name = client.Name,
                                    Type = client.Type,
                                    Node = node
                                });
        }
    }
}