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
        public ClientEventManager(string eventName, TwinoMQ server)
            : base(server, eventName, null)
        {
        }

        /// <summary>
        /// Triggers client connected or disconnected events
        /// </summary>
        public void Trigger(MqClient client, string node = null)
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