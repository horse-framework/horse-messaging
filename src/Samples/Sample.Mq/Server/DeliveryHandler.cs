using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Sample.Mq.Server
{
    public class DeliveryHandler : IMessageDeliveryHandler
    {
        public async Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            Console.WriteLine($"Message received to {queue.ContentType} queue in {queue.Channel.Name}");
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> BeginSend(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message send operation is starting in {queue.ContentType} queue in {queue.Channel.Name}");
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<bool> CanConsumerReceive(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            Console.WriteLine($"Message is going to to {receiver.UniqueId} in {queue.ContentType} queue in {queue.Channel.Name}");
            return await Task.FromResult(true);
        }

        public async Task ConsumerReceived(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            Console.WriteLine($"Message is sent to {receiver.UniqueId} in {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Send operation completed in {queue.ContentType} queue in {queue.Channel.Name}");
            return await Task.FromResult(new Decision(true, true));
        }

        public async Task AcknowledgeReceived(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery)
        {
            Console.WriteLine($"Acknowledge received in {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task MessageTimedOut(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message timed out in {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task AcknowledgeTimedOut(ChannelQueue queue, MessageDelivery delivery)
        {
            Console.WriteLine($"Acknowledge timed out in {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task MessageRemoved(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message removed from {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task ExceptionThrown(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            Console.WriteLine("Exception thrown: " + exception);
            await Task.CompletedTask;
        }

        public async Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"{queue.ContentType} message saved in {queue.Channel.Name}");
            return await Task.FromResult(true);
        }
    }
}