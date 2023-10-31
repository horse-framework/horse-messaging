using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace AdvancedSample.Server.Implementations.Other
{
    public class CustomServerMessageHandler : IServerMessageHandler
    {
        public Task Received(MessagingClient client, HorseMessage message)
        {
            return Task.CompletedTask;
        }
    }
}
