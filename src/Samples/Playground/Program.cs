using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.Protocols.TMQ;
using Xunit;

namespace Playground
{
    class Program
    {
        private static volatile int X = 0;

        static void Main(string[] args)
        {
            A().Wait();
            Console.ReadLine();
        }

        private static async Task A()
        {
            int port = 49400;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300);

            Channel channel = server.Server.FindChannel("ch-1");
            Assert.NotNull(channel);
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);

            queue.Options.HideClientNames = true;
            queue.Options.RequestAcknowledge = true;
            queue.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(15);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            client.AutoAcknowledge = true;
            client.CatchAcknowledgeMessages = true;

            bool joined = await client.Join("ch-1", true);

            TmqMessage received = null;
            TmqMessage ack = null;
            client.MessageReceived += (c, m) =>
            {
                switch (m.Type)
                {
                    case MessageType.Channel:
                        received = m;
                        break;
                    case MessageType.Acknowledge:
                        ack = m;
                        break;
                }
            };

            await Task.Delay(500);

            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));
            bool sent = await client.Push("ch-1", MessageA.ContentType, ms, true);

            await Task.Delay(1000);
        }
    }
}