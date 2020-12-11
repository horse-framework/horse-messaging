using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Queues;
using Horse.Mq.Routing;
using Horse.Protocols.Hmq;
using Horse.Protocols.Hmq.Models;

namespace Horse.Mq.Security
{
    /// <summary>
    /// Implementation of the object that checks authority of client operations
    /// </summary>
    public interface IClientAuthorization
    {
        /// <summary>
        /// Returns true, if client can create new queue
        /// </summary>
        Task<bool> CanCreateQueue(MqClient client, string name, NetworkOptionsBuilder options);

        /// <summary>
        /// Returns true, if client can send a peer message
        /// </summary>
        Task<bool> CanDirectMessage(MqClient sender, HorseMessage message, MqClient receiver);

        /// <summary>
        /// Returns true, if client can send a message to the queue
        /// </summary>
        Task<bool> CanMessageToQueue(MqClient client, HorseQueue queue, HorseMessage message);

        /// <summary>
        /// Returns true, if client can pull a message from the queue
        /// </summary>
        Task<bool> CanPullFromQueue(QueueClient client, HorseQueue queue);

        /// <summary>
        /// Returns true, if client can subscribe to the event
        /// </summary>
        bool CanSubscribeEvent(MqClient client, HorseQueue queue);

        /// <summary>
        /// Returns true, if client can create a router
        /// </summary>
        Task<bool> CanCreateRouter(MqClient client, string routerName, RouteMethod method);

        /// <summary>
        /// Returns true, if client can remove a router
        /// </summary>
        Task<bool> CanRemoveRouter(MqClient client, IRouter router);
        
        /// <summary>
        /// Returns true, if client can create a binding in a router
        /// </summary>
        Task<bool> CanCreateBinding(MqClient client, IRouter router, BindingInformation binding);

        /// <summary>
        /// Returns true, if client can remove a binding from a router
        /// </summary>
        Task<bool> CanRemoveBinding(MqClient client, Binding binding);
    }
}