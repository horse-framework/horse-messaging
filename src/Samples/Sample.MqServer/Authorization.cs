using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;

namespace Sample.MqServer
{
    public class Authorization : IClientAuthorization
    {
        public async Task<bool> CanCreateChannel(MqClient client, Twino.MQ.MqServer server, string channelName)
        {
            Console.WriteLine("Can create new channel passed");
            return await Task.FromResult(true);
        }

        public async Task<bool> CanCreateQueue(MqClient client, Channel channel, ushort contentType)
        {
            Console.WriteLine("Can create new queue passed");
            return await Task.FromResult(true);
        }

        public async Task<bool> CanMessageToPeer(MqClient sender, TmqMessage message, MqClient receiver)
        {
            Console.WriteLine("Can message to peer passed");
            return await Task.FromResult(true);
        }

        public async Task<bool> CanMessageToQueue(MqClient client, ChannelQueue queue, TmqMessage message)
        {
            Console.WriteLine("Can message to queue passed");
            return await Task.FromResult(true);
        }
    }
}