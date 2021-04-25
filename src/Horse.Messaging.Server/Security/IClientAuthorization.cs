using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;

namespace Horse.Messaging.Server.Security
{
    /// <summary>
    /// Implementation of the object that checks authority of client operations
    /// </summary>
    public interface IClientAuthorization
    {
        /// <summary>
        /// Returns true, if client can create new queue
        /// </summary>
        Task<bool> CanCreateQueue(MessagingClient client, string name, NetworkOptionsBuilder options);

        /// <summary>
        /// Returns true, if client can send a peer message
        /// </summary>
        Task<bool> CanDirectMessage(MessagingClient sender, HorseMessage message, MessagingClient receiver);

        /// <summary>
        /// Returns true, if client can send a message to the queue
        /// </summary>
        Task<bool> CanMessageToQueue(MessagingClient client, HorseQueue queue, HorseMessage message);

        /// <summary>
        /// Returns true, if client can pull a message from the queue
        /// </summary>
        Task<bool> CanPullFromQueue(QueueClient client, HorseQueue queue);

        /// <summary>
        /// Returns true, if client can subscribe to the event
        /// </summary>
        bool CanSubscribeEvent(MessagingClient client, HorseEventType eventType, string target);

        /// <summary>
        /// Returns true, if client can create a router
        /// </summary>
        Task<bool> CanCreateRouter(MessagingClient client, string routerName, RouteMethod method);

        /// <summary>
        /// Returns true, if client can remove a router
        /// </summary>
        Task<bool> CanRemoveRouter(MessagingClient client, IRouter router);
        
        /// <summary>
        /// Returns true, if client can create a binding in a router
        /// </summary>
        Task<bool> CanCreateBinding(MessagingClient client, IRouter router, BindingInformation binding);

        /// <summary>
        /// Returns true, if client can remove a binding from a router
        /// </summary>
        Task<bool> CanRemoveBinding(MessagingClient client, Binding binding);
    }
}