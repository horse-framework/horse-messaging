using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Queues;
using Horse.Mq.Security;
using Horse.Protocols.Hmq;

namespace Test.Common.Handlers
{
    public class TestAdminAuthorization : IAdminAuthorization
    {
        public Task<bool> CanUpdateQueueOptions(MqClient client, HorseQueue queue, NetworkOptionsBuilder options)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanRemoveQueue(MqClient client, HorseQueue queue)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanClearQueueMessages(MqClient client, HorseQueue queue, bool priorityMessages, bool messages)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanManageInstances(MqClient client, HorseMessage request)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanReceiveClients(MqClient client)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanReceiveQueues(MqClient client)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanReceiveQueueConsumers(MqClient client, HorseQueue queue)
        {
            return Task.FromResult(true);
        }
    }
}