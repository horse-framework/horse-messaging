using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
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

        public async Task<bool> CanRemoveChannel(MqClient client, MqServer server, Channel channel)
        {
            return await Task.FromResult(true);
        }

        public async Task<bool> CanCreateQueue(MqClient client, Channel channel, ushort contentType, NetworkOptionsBuilder options)
        {
            bool grant = client.Type.Equals("producer");
            Console.WriteLine("Can create new queue: " + grant);
            return await Task.FromResult(grant);
        }

        public async Task<bool> CanUpdateQueueOptions(MqClient client, Channel channel, ChannelQueue queue, NetworkOptionsBuilder options)
        {
            return await Task.FromResult(true);
        }

        public async Task<bool> CanRemoveQueue(MqClient client, ChannelQueue queue)
        {
            return await Task.FromResult(true);
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

        public async Task<bool> CanPullFromQueue(ChannelClient client, ChannelQueue queue)
        {
            Console.WriteLine("can pull from queue True");
            return await Task.FromResult(true);
        }
    }
}