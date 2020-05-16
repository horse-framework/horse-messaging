using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Core.Protocols;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Queues;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.Http;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;
using Twino.Server;

namespace Playground
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TmqMessage msg = new TmqMessage(MessageType.QueueMessage);
            msg.AddHeader("hello", "test");
            msg.SetStringContent("Hello world!");
            msg.SetSource("kiya");
            msg.SetTarget("foo");

            byte[] d = TmqWriter.Create(msg);
            TmqReader reader = new TmqReader();
            TmqMessage msg2 = await reader.Read(new MemoryStream(d));
            Console.WriteLine(msg2.FindHeader("hello"));
            return;
            
            int queueCount = 1;

            DeliveryHandler[] handlers = new DeliveryHandler[queueCount];
            for (int i = 0; i < queueCount; i++)
            {
                handlers[i] = new DeliveryHandler(i);
                await handlers[i].Init();
            }

            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
            server.Options.Hosts[0].Port = 26223;

            MqServer mq = new MqServer();
            mq.SetDefaultDeliveryHandler(handlers[0]);

            server.UseMqServer(mq);
            for (int i = 0; i < queueCount; i++)
            {
                var channel = mq.CreateChannel("Test" + i);
                var queue = await channel.CreateQueue(100, channel.Options, handlers[i]);
                queue.Options.WaitForAcknowledge = false;
                await queue.SetStatus(QueueStatus.Broadcast);
            }

            server.Start();

            int seconds = 0;
            Thread thread = new Thread(async () =>
            {
                while (true)
                {
                    long prev = 0;
                    for (int i = 0; i < queueCount; i++)
                        prev += handlers[i].Count;

                    await Task.Delay(1000);

                    long current = 0;
                    for (int i = 0; i < queueCount; i++)
                        current += handlers[i].Count;

                    long diff = current - prev;
                    seconds++;
                    Console.WriteLine($"{diff} m/s \t {current} total \t {seconds} secs");
                }
            });
            thread.Start();

            for (int i = 0; i < queueCount; i++)
                _ = RunProducer("Test" + i, 100);

            await server.BlockWhileRunningAsync();
        }

        private static async Task RunProducer(string channel, ushort queue)
        {
            string x = new string('a', 1);
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(x));
            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://127.0.0.1:26223");

            while (true)
            {
                ms.Position = 0;
                await client.Push(channel, queue, ms, false);
                //await Task.Delay(2);
            }
        }
    }
}