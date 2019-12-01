using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;

namespace Sample.MqServer
{
    public class Authorization : IClientAuthorization
    {
        public async Task<bool> CanCreateChannel(MqClient client, MQServer server, string channelName)
        {
            return await Task.FromResult(true);
        }

        public async Task<bool> CanCreateQueue(MqClient client, Channel channel, ushort contentType)
        {
            return await Task.FromResult(true);
        }

        public async Task<bool> CanMessageToPeer(MqClient sender, TmqMessage message, MqClient receiver)
        {
            return await Task.FromResult(true);
        }

        public async Task<bool> CanMessageToQueue(MqClient client, ChannelQueue queue, TmqMessage message)
        {
            return await Task.FromResult(true);
        }
    }
}