using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace ClusteringSample.Server
{
    public class QueueMessageHandler : IQueueMessageEventHandler
    {
        public Task OnProduced(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            Console.WriteLine($"PRODUCE {message.Message}");
            return Task.CompletedTask;
        }

        public Task OnConsumed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.CompletedTask;
        }

        public Task OnAcknowledged(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            return Task.CompletedTask;
        }

        public Task MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            return Task.CompletedTask;
        }

        public Task OnAcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            return Task.CompletedTask;
        }
    }
}