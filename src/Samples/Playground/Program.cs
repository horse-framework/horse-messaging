using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Twino.Server;
using Xunit;

namespace Playground
{
    public class SampleMessageDelivery : IMessageDeliveryHandler
    {
        public async Task<Decision> ReceivedFromProducer(ChannelQueue queue,
                                                         QueueMessage message,
                                                         MqClient sender)
        {
            Console.WriteLine($"A mesage is received: " + message.Message);
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> BeginSend(ChannelQueue queue,
                                              QueueMessage message)
        {
            Console.WriteLine("Message is about to send to consumers");
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<bool> CanConsumerReceive(ChannelQueue queue,
                                                   QueueMessage message,
                                                   MqClient receiver)
        {
            Console.WriteLine($"Message is about to send to {receiver.Name}");
            return await Task.FromResult(true);
        }

        public async Task ConsumerReceived(ChannelQueue queue,
                                           MessageDelivery delivery,
                                           MqClient receiver)
        {
            Console.WriteLine($"Message is sent to {receiver.Name}");
            await Task.CompletedTask;
        }

        public async Task<Decision> EndSend(ChannelQueue queue,
                                            QueueMessage message)
        {
            Console.WriteLine($"Message send operation has completed");
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task AcknowledgeReceived(ChannelQueue queue,
                                              TmqMessage acknowledgeMessage,
                                              MessageDelivery delivery)
        {
            Console.WriteLine($"Acknowledge is received: {delivery.Message.Message}");
            await Task.CompletedTask;
        }

        public async Task MessageTimedOut(ChannelQueue queue,
                                          QueueMessage message)
        {
            Console.WriteLine("There are no receivers, message time is out");
            await Task.CompletedTask;
        }

        public async Task AcknowledgeTimedOut(ChannelQueue queue, 
                                              MessageDelivery delivery)
        {
            Console.WriteLine("Queue was requesting ack but no acknowledge received for the message");
            await Task.CompletedTask;
        }

        public async Task MessageRemoved(ChannelQueue queue,
                                         QueueMessage message)
        {
            Console.WriteLine($"Message is removed: {message.Message}");
            await Task.CompletedTask;
        }

        public async Task ExceptionThrown(ChannelQueue queue,
                                          QueueMessage message,
                                          Exception exception)
        {
            Console.WriteLine($"An exception is thrown on delivery: {exception}");
            await Task.CompletedTask;
        }

        public async Task<bool> SaveMessage(ChannelQueue queue, 
                                            QueueMessage message)
        {
            Console.WriteLine("Message save method is called");
            return await Task.FromResult(false);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
        }
    }
}