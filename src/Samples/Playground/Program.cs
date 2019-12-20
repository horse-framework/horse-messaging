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
using Twino.Protocols.TMQ;
using Twino.Server;
using Xunit;

namespace Playground
{
    public class SampleMessageDelivery : IMessageDeliveryHandler
    {
        public async Task<MessageDecision> OnReceived(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            Console.WriteLine($"A mesage is received: " + message.Message);
            return await Task.FromResult(MessageDecision.Allow);
        }

        public async Task<MessageDecision> OnSendStarting(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine("Message is about to send to consumers");
            return await Task.FromResult(MessageDecision.Allow);
        }

        public async Task<DeliveryDecision> OnBeforeSend(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            Console.WriteLine($"Message is about to send to {receiver.Name}");
            return await Task.FromResult(DeliveryDecision.Allow);
        }

        public async Task OnAfterSend(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            Console.WriteLine($"Message is sent to {receiver.Name}");
            await Task.CompletedTask;
        }

        public async Task<DeliveryOperation> OnSendCompleted(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message send operation has completed");
            return await Task.FromResult(DeliveryOperation.DontSaveMesage);
        }

        public async Task OnAcknowledge(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery)
        {
            Console.WriteLine($"Acknowledge is received for the message: {delivery.Message.Message}");
            await Task.CompletedTask;
        }

        public async Task OnTimeUp(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine("There are no receivers, message time is out, will be removed");
            await Task.CompletedTask;
        }

        public async Task OnAcknowledgeTimeUp(ChannelQueue queue, MessageDelivery delivery)
        {
            Console.WriteLine("Queue was requesting ack but no acknowledge received for the message");
            await Task.CompletedTask;
        }

        public async Task OnRemove(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine($"Message is removed: {message.Message}");
            await Task.CompletedTask;
        }

        public async Task OnException(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            Console.WriteLine($"An exception is thrown on delivery: {exception}");
            await Task.CompletedTask;
        }

        public async Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            Console.WriteLine("Message save method is called");
            return await Task.FromResult(false);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            CreateServer();
            
            Console.WriteLine("Press enter to connect to the MQ server");
            Console.ReadLine();

            ConnectAndPush();

            Console.ReadLine();
        }

        static void CreateServer()
        {
            IPHostEntry hostEntry = Dns.GetHostEntry("www.google.com");
            foreach (IPAddress address in hostEntry.AddressList)
            {
                Console.WriteLine(address);
            }

            Console.ReadLine();
            string ip = hostEntry.AddressList[0].ToString();
            Console.WriteLine(ip);
            Console.ReadLine();
            return;
            
            //create messaging queue server
            SampleMessageDelivery delivery = new SampleMessageDelivery();
            MqServer mq = new MqServer();
            mq.SetDefaultDeliveryHandler(delivery);

            //create channel and queue
            Channel channel = mq.CreateChannel("ch");
            channel.CreateQueue(10).Wait();

            //create twino server, use mq and listen port 26222
            TwinoServer server = new TwinoServer();
            server.UseMqServer(mq);
            server.Start(26222);
        }

        static void ConnectAndPush()
        {
            TmqClient client = new TmqClient();
            client.SetClientName("demo-client");
            client.Connect("tmq://localhost:26222");
            client.MessageReceived += (c, msg) => Console.WriteLine($"Received from the queue: {msg}");

            bool joined = client.Join("ch", true).Result;
            Console.WriteLine($"Channel join: {joined}");

            client.Push("ch", 10, "Hello, World!", false);
        }
    }
}