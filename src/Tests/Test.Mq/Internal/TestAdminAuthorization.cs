using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;

namespace Test.Mq.Internal
{
    public class TestAdminAuthorization : IAdminAuthorization
    {
        public Task<bool> CanRemoveChannel(MqClient client, TwinoMQ server, Channel channel)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanUpdateQueueOptions(MqClient client, Channel channel, TwinoQueue queue, NetworkOptionsBuilder options)
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

        public Task<bool> CanReceiveChannelInfo(MqClient client, Channel channel)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanReceiveChannelConsumers(MqClient client, Channel channel)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanReceiveChannelQueues(MqClient client, Channel channel)
        {
            return Task.FromResult(true);
        }
    }
}