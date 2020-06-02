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
        public Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            Console.WriteLine($"Message received to {queue.Id} queue in {queue.Channel.Name}");
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> BeginSend(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message send operation is starting in {queue.Id} queue in {queue.Channel.Name}");
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> CanConsumerReceive(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            Console.WriteLine($"Message is going to to {receiver.UniqueId} in {queue.Id} queue in {queue.Channel.Name}");
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> ConsumerReceived(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            Console.WriteLine($"Message is sent to {receiver.UniqueId} in {queue.Id} queue in {queue.Channel.Name}");
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> ConsumerReceiveFailed(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            Console.WriteLine($"Message sent failed to {receiver.UniqueId} in {queue.Id} queue in {queue.Channel.Name}");
            return Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Send operation completed in {queue.Id} queue in {queue.Channel.Name}");
            return await Task.FromResult(new Decision(true, true));
        }

        public Task<Decision> AcknowledgeReceived(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            Console.WriteLine($"Acknowledge received in {queue.Id} queue in {queue.Channel.Name}");
            return Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.IfSaved));
        }

        public async Task<Decision> MessageTimedOut(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message timed out in {queue.Id} queue in {queue.Channel.Name}");
            return await Task.FromResult(new Decision(true, true));
        }

        public Task<Decision> AcknowledgeTimedOut(ChannelQueue queue, MessageDelivery delivery)
        {
            Console.WriteLine($"Acknowledge timed out in {queue.Id} queue in {queue.Channel.Name}");
            return Task.FromResult(new Decision(true, true));
        }

        public Task MessageRemoved(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message removed from {queue.Id} queue in {queue.Channel.Name}");
            return Task.CompletedTask;
        }

        public Task<Decision> ExceptionThrown(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            Console.WriteLine("Exception thrown: " + exception);
            return Task.FromResult(new Decision(true, true));
        }

        public Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"{queue.Id} message saved in {queue.Channel.Name}");
            return Task.FromResult(true);
        }
    }
}