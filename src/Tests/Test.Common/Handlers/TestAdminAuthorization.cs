using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;

namespace Test.Common.Handlers
{
    public class TestAdminAuthorization : IAdminAuthorization
    {
        public Task<bool> CanUpdateQueueOptions(MqClient client, TwinoQueue queue, NetworkOptionsBuilder options)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanRemoveQueue(MqClient client, TwinoQueue queue)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanClearQueueMessages(MqClient client, TwinoQueue queue, bool priorityMessages, bool messages)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanManageInstances(MqClient client, TwinoMessage request)
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

        public Task<bool> CanReceiveQueueConsumers(MqClient client, TwinoQueue queue)
        {
            return Task.FromResult(true);
        }
    }
}