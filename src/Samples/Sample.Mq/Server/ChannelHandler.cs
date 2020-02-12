using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Sample.Mq.Server
{
    public class ChannelHandler : IChannelEventHandler
    {
        public async Task OnQueueCreated(ChannelQueue queue, Channel channel)
        {
            Console.WriteLine($"{queue.Id} is created in {channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnQueueRemoved(ChannelQueue queue, Channel channel)
        {
            Console.WriteLine($"{queue.Id} queue removed from {channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnClientJoined(ChannelClient client)
        {
            Console.WriteLine($"{client.Client.UniqueId} joined to {client.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnClientLeft(ChannelClient client)
        {
            Console.WriteLine($"{client.Client.UniqueId} left from {client.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnChannelCreated(Channel channel)
        {
            Console.WriteLine($"Channel is created {channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnChannelRemoved(Channel channel)
        {
            Console.WriteLine($"Channel is removed {channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnQueueStatusChanged(ChannelQueue queue, QueueStatus from, QueueStatus to)
        {
            Console.WriteLine($"{queue.Id} queue status changed from {from} to {to}");
            await Task.CompletedTask;
        }
    }
}