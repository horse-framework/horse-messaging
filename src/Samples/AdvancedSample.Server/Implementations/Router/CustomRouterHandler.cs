using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace AdvancedSample.Server.Implementations.Router
{
    public class CustomRouterHandler : IRouterMessageHandler
    {
        public Task OnNotRouted(MessagingClient sender, Horse.Messaging.Server.Routing.Router router, HorseMessage message)
        {
            return Task.CompletedTask;
        }

        public Task OnRouted(MessagingClient sender, Horse.Messaging.Server.Routing.Router router, HorseMessage message)
        {
            return Task.CompletedTask;
        }

        public Task OnRouterNotFound(MessagingClient sender, HorseMessage message)
        {
            return Task.CompletedTask;
        }
    }
}
