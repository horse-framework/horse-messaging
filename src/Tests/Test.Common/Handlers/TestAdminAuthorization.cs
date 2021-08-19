using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Security;

namespace Test.Common.Handlers
{
    public class TestAdminAuthorization : IAdminAuthorization
    {
        public Task<bool> CanUpdateQueueOptions(MessagingClient client, HorseQueue queue, NetworkOptionsBuilder options)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanRemoveQueue(MessagingClient client, HorseQueue queue)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanClearQueueMessages(MessagingClient client, HorseQueue queue, bool priorityMessages, bool messages)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanManageInstances(MessagingClient client, HorseMessage request)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanReceiveClients(MessagingClient client)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanReceiveQueues(MessagingClient client)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanReceiveQueueConsumers(MessagingClient client, HorseQueue queue)
        {
            return Task.FromResult(true);
        }
    }
}