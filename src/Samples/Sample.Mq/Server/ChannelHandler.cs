using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;

namespace Sample.Mq.Server
{
    public class ChannelHandler : IChannelEventHandler
    {
        public async Task OnQueueCreated(ChannelQueue queue, Channel channel)
        {
            Console.WriteLine($"{queue.ContentType} is created in {channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnQueueRemoved(ChannelQueue queue, Channel channel)
        {
            Console.WriteLine($"{queue.ContentType} queue removed from {channel.Name}");
            await Task.CompletedTask;
        }

        public async Task ClientJoined(ChannelClient client)
        {
            Console.WriteLine($"{client.Client.UniqueId} joined to {client.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task ClientLeft(ChannelClient client)
        {
            Console.WriteLine($"{client.Client.UniqueId} left from {client.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task<bool> OnChannelStatusChanged(Channel channel, ChannelStatus from, ChannelStatus to)
        {
            Console.WriteLine($"{channel.Name} channel status changed from {from} to {to}");
            return await Task.FromResult(true);
        }

        public async Task<bool> OnQueueStatusChanged(ChannelQueue queue, QueueStatus from, QueueStatus to)
        {
            Console.WriteLine($"{queue.ContentType} queue status changed from {from} to {to}");
            return await Task.FromResult(true);
        }
    }
}