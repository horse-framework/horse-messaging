using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;

namespace AdvancedSample.Server.Implementations.Client
{
    public class CustomClientAuthorizer : IClientAuthorization
    {
        public async Task<bool> CanCreateBinding(MessagingClient client, Horse.Messaging.Server.Routing.Router router, BindingInformation binding)
        {
            return await Task.Run(() =>
            {
                return true;
            });
        }
        public async Task<bool> CanCreateQueue(MessagingClient client, string name, NetworkOptionsBuilder options)
        {
            return await Task.Run(() =>
            {
                return true;
            });
        }
        public async Task<bool> CanCreateRouter(MessagingClient client, string routerName, RouteMethod method)
        {
            return await Task.Run(() =>
            {
                return true;
            });
        }

        public async Task<bool> CanDirectMessage(MessagingClient sender, HorseMessage message, MessagingClient receiver)
        {
            return await Task.Run(() =>
            {
                return true;
            });
        }
        public async Task<bool> CanMessageToQueue(MessagingClient client, HorseQueue queue, HorseMessage message)
        {
            return await Task.Run(() =>
            {
                return true;
            });
        }
        public async Task<bool> CanPullFromQueue(QueueClient client, HorseQueue queue)
        {

            return await Task.Run(() =>
            {
                return true;
            });
        }
        public async Task<bool> CanRemoveBinding(MessagingClient client, Binding binding)
        {
            return await Task.Run(() =>
            {
                return true;
            });
        }
        public async Task<bool> CanRemoveRouter(MessagingClient client, Horse.Messaging.Server.Routing.Router router)
        {
            return await Task.Run(() =>
            {
                return true;
            });
        }
        public bool CanSubscribeEvent(MessagingClient client, HorseEventType eventType, string target)
        {
            return false;
        }
    }
}
