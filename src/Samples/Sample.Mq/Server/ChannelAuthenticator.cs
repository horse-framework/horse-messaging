using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Security;

namespace Sample.Mq.Server
{
    public class ChannelAuthenticator : IChannelAuthenticator
    {
        public async Task<bool> Authenticate(Channel channel, MqClient client)
        {
            Console.WriteLine($"{client.UniqueId} authenticated in {channel.Name}");
            return await Task.FromResult(true);
        }
    }
}