using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Security;

namespace Sample.MqServer
{
    public class ChannelAuthenticator : IChannelAuthenticator
    {
        public async Task<bool> Authenticate(Channel channel, MqClient client)
        {
            return await Task.FromResult(true);
        }
    }
}