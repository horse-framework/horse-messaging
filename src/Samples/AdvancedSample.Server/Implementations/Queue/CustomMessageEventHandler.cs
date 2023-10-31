using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace AdvancedSample.Server.Implementations.Queue
{
    public class CustomMessageEventHandler : IQueueMessageEventHandler
    {
        public  Task MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            return Task.CompletedTask;
        }

        public  Task OnAcknowledged(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            Console.WriteLine(delivery.Acknowledge);
            return Task.CompletedTask;
        }

        public  Task OnAcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            return Task.CompletedTask;
        }

        public  Task OnConsumed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            Console.WriteLine($"sent to consumer: {receiver.Name}");
            return Task.CompletedTask;
        }

        public Task OnProduced(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            Console.WriteLine($"sent from producer: {sender.Name} to queue: {queue.Name}");
            Console.WriteLine(message.IsSaved);
            Console.WriteLine(queue.Type);
            Console.WriteLine(message.Message.GetStringContent());
            Console.WriteLine(DateTime.Now);
            Console.WriteLine("");

            return Task.CompletedTask;
        }
    }
}
