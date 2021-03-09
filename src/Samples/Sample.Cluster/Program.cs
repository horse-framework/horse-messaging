using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Horse.Mq;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;
using Horse.Mq.Delivery;
using Horse.Mq.Handlers;
using Horse.Mq.Options;
using Horse.Mq.Queues;
using Horse.Protocols.Hmq;
using Horse.Server;

namespace Sample.Cluster
{
    class Program
    {
        static async Task Main(string[] args)
        {
            StartServer2();
            StartServer1();
            
            await Task.Delay(1500);

            ConnectToServer1AsProducer();
            ConnectToServer2AsConsumer();
            
            Console.ReadLine();
        }

        static HorseMq StartServer1()
        {
            HorseMq mq = HorseMqBuilder.Create()
                                       .AddOptions(o => o.Status = QueueStatus.Push)
                                       .UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No)
                                       .Build();

            mq.NodeManager.SetHost(new HostOptions {Port = 26101});
            mq.NodeManager.AddRemoteNode(new NodeOptions
                                         {
                                             Host = "hmq://localhost:26100",
                                             Name = "Node-2",
                                             KeepMessages = false,
                                             ReconnectWait = 500
                                         });

            HorseServer server = new HorseServer();
            server.UseHorseMq(mq);
            server.Start(26001);
            return mq;
        }

        static HorseMq StartServer2()
        {
            HorseMq mq = HorseMqBuilder.Create()
                                       .AddOptions(o => o.Status = QueueStatus.Push)
                                       .UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No)
                                       .Build();

            mq.NodeManager.SetHost(new HostOptions {Port = 26100});
            mq.NodeManager.AddRemoteNode(new NodeOptions
                                         {
                                             Host = "hmq://localhost:26101",
                                             Name = "Node-2",
                                             KeepMessages = false,
                                             ReconnectWait = 500
                                         });

            HorseServer server = new HorseServer();
            server.UseHorseMq(mq);
            server.Start(26002);
            return mq;
        }

        static void ConnectToServer1AsProducer()
        {
            HmqStickyConnector producer = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            producer.AddHost("hmq://localhost:26002");
            
            producer.ContentSerializer = new NewtonsoftContentSerializer();
            producer.Run();

            Task.Run(async () =>
            {
                int counter = 1;
                while (true)
                {
                    await Task.Delay(1000);

                    MirroredModel model = new MirroredModel();
                    model.Foo = $"Foo #{counter}";

                    HorseResult pushResult = await producer.Bus.Queue.PushJson(model, true);
                    Console.WriteLine($"Push Result: {pushResult.Code}");

                    counter++;
                }
            });
        }

        static void ConnectToServer2AsConsumer()
        {
            HmqStickyConnector consumer = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            consumer.AddHost("hmq://localhost:26001");
            consumer.ContentSerializer = new NewtonsoftContentSerializer();
            consumer.Observer.RegisterConsumer<MirroredModelConsumer>();
            consumer.Run();
            
            HmqStickyConnector consumer1 = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            consumer1.AddHost("hmq://localhost:26002");
            consumer1.ContentSerializer = new NewtonsoftContentSerializer();
            consumer1.Observer.RegisterConsumer<MirroredModelConsumer>();
            consumer1.Run();
        }
    }
}