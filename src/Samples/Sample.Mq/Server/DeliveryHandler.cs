using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Sample.Mq.Server
{
    public class DeliveryHandler : IMessageDeliveryHandler
    {
        public async Task<MessageDecision> OnReceived(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            Console.WriteLine($"Message received to {queue.ContentType} queue in {queue.Channel.Name}");
            return await Task.FromResult(MessageDecision.Allow);
        }

        public async Task<MessageDecision> OnSendStarting(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message send operation is starting in {queue.ContentType} queue in {queue.Channel.Name}");
            return await Task.FromResult(MessageDecision.Allow);
        }

        public async Task<DeliveryDecision> OnBeforeSend(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            Console.WriteLine($"Message is going to to {receiver.UniqueId} in {queue.ContentType} queue in {queue.Channel.Name}");
            return await Task.FromResult(DeliveryDecision.Allow);
        }

        public async Task OnAfterSend(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            Console.WriteLine($"Message is sent to {receiver.UniqueId} in {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task<DeliveryOperation> OnSendCompleted(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Send operation completed in {queue.ContentType} queue in {queue.Channel.Name}");
            return await Task.FromResult(DeliveryOperation.SaveMessage);
        }

        public async Task OnAcknowledge(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery)
        {
            Console.WriteLine($"Acknowledge received in {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnTimeUp(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message timed out in {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnAcknowledgeTimeUp(ChannelQueue queue, MessageDelivery delivery)
        {
            Console.WriteLine($"Acknowledge timed out in {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnRemove(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message removed from {queue.ContentType} queue in {queue.Channel.Name}");
            await Task.CompletedTask;
        }

        public async Task OnException(ChannelQueue queue, QueueMessage message, Exception exception)
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