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
        public Task<bool> CanRemoveChannel(MqClient client, MqServer server, Channel channel)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanUpdateQueueOptions(MqClient client, Channel channel, ChannelQueue queue, NetworkOptionsBuilder options)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanRemoveQueue(MqClient client, ChannelQueue queue)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CanManageInstances(MqClient client, TmqMessage request)
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