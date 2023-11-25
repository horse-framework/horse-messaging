using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Security;

namespace AdvancedSample.Server.Implementations.Client;

public class CustomAdminClientAuthorizer : IAdminAuthorization
{
    public async Task<bool> CanClearQueueMessages(MessagingClient client, HorseQueue queue, bool priorityMessages, bool messages)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanManageInstances(MessagingClient client, HorseMessage request)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanReceiveChannelSubscribers(MessagingClient client, HorseChannel channel)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanReceiveClients(MessagingClient client)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanReceiveQueueConsumers(MessagingClient client, HorseQueue queue)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanReceiveQueues(MessagingClient client)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanRemoveQueue(MessagingClient client, HorseQueue queue)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CanUpdateQueueOptions(MessagingClient client, HorseQueue queue, NetworkOptionsBuilder options)
    {
        throw new NotImplementedException();
    }
}