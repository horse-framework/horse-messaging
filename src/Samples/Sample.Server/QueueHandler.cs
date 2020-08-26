using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Sample.Server
{
    public class QueueHandler : IQueueEventHandler
    {
        public Task OnQueueCreated(ChannelQueue queue, Channel channel)
        {
            Console.WriteLine("Queue Created");
            return Task.CompletedTask;
        }

        public Task OnQueueRemoved(ChannelQueue queue, Channel channel)
        {
            return Task.CompletedTask;
        }

        public Task OnClientJoined(QueueClient client)
        {
            Console.WriteLine("Client Joined");
            return Task.CompletedTask;
        }

        public Task OnClientLeft(QueueClient client)
        {
            Console.WriteLine("Client Left");
            return Task.CompletedTask;
        }

        public Task OnQueueStatusChanged(ChannelQueue queue, QueueStatus @from, QueueStatus to)
        {
            return Task.CompletedTask;
        }

        public Task OnChannelCreated(Channel channel)
        {
            return Task.CompletedTask;
        }

        public Task OnChannelRemoved(Channel channel)
        {
            return Task.CompletedTask;
        }
    }
}