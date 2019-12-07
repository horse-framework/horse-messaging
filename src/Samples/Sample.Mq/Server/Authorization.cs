using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;

namespace Sample.Mq.Server
{
    public class Authorization : IClientAuthorization
    {
        public async Task<bool> CanCreateChannel(MqClient client, MqServer server, string channelName)
        {
            bool grant = client.Type.Equals("producer");
            Console.WriteLine("Can create new channel: " + grant);
            return await Task.FromResult(grant);
        }

        public async Task<bool> CanCreateQueue(MqClient client, Channel channel, ushort contentType)
        {
            bool grant = client.Type.Equals("producer");
            Console.WriteLine("Can create new queue: " + grant);
            return await Task.FromResult(grant);
        }

        public async Task<bool> CanMessageToPeer(MqClient sender, TmqMessage message, MqClient receiver)
        {
            Console.WriteLine("Can message to peer passed");
            return await Task.FromResult(true);
        }

        public async Task<bool> CanMessageToQueue(MqClient client, ChannelQueue queue, TmqMessage message)
        {
            bool grant = client.Type.Equals("producer");
            Console.WriteLine("Can message to queue: " + grant);
            return await Task.FromResult(grant);
        }
    }
}