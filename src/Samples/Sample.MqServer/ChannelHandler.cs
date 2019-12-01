using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;

namespace Sample.MqServer
{
    public class ChannelHandler : IChannelEventHandler
    {
        public Task OnQueueCreated(ChannelQueue queue, Channel channel)
        {
            throw new System.NotImplementedException();
        }

        public Task OnQueueRemoved(ChannelQueue queue, Channel channel)
        {
            throw new System.NotImplementedException();
        }

        public Task ClientJoined(ChannelClient client)
        {
            throw new System.NotImplementedException();
        }

        public Task ClientLeft(ChannelClient client)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> OnChannelStatusChanged(Channel channel, ChannelStatus @from, ChannelStatus to)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> OnQueueStatusChanged(ChannelQueue queue, QueueStatus @from, QueueStatus to)
        {
            throw new System.NotImplementedException();
        }
    }
}