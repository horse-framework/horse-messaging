using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Client;
using Horse.Messaging.Server.Handlers;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Protocol;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;
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

        static HorseRider StartServer1()
        {
            HorseRider rider = HorseRiderBuilder.Build()
                                       .AddOptions(o => o.Status = QueueStatus.Push)
                                       .UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No)
                                       .Build();

            rider.NodeManager.SetHost(new HostOptions {Port = 26101});
            rider.NodeManager.AddRemoteNode(new NodeOptions
                                         {
                                             Host = "horse://localhost:26100",
                                             Name = "Node-2",
                                             KeepMessages = false,
                                             ReconnectWait = 500
                                         });

            HorseServer server = new HorseServer();
            server.UseRider(rider);
            server.Start(26001);
            return rider;
        }

        static HorseRider StartServer2()
        {
            HorseRider rider = HorseRiderBuilder.Build()
                                       .AddOptions(o => o.Status = QueueStatus.Push)
                                       .UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No)
                                       .Build();

            rider.NodeManager.SetHost(new HostOptions {Port = 26100});
            rider.NodeManager.AddRemoteNode(new NodeOptions
                                         {
                                             Host = "horse://localhost:26101",
                                             Name = "Node-1",
                                             KeepMessages = false,
                                             ReconnectWait = 500
                                         });

            HorseServer server = new HorseServer();
            server.UseRider(rider);
            server.Start(26002);
            return rider;
        }

        static void ConnectToServer1AsProducer()
        {
            HmqStickyConnector producer = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            producer.AddHost("horse://localhost:26002");

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
            //that client is connected to same node with producer
            HmqStickyConnector consumer1 = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            consumer1.AddHost("horse://localhost:26002");
            consumer1.ContentSerializer = new NewtonsoftContentSerializer();
            consumer1.Observer.RegisterConsumer<MirroredModelConsumer>();
            consumer1.Run();

            //that client is connected to other node
            HmqStickyConnector consumer2 = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            consumer2.AddHost("horse://localhost:26001");
            consumer2.ContentSerializer = new NewtonsoftContentSerializer();
            consumer2.Observer.RegisterConsumer<MirroredModelConsumer>();
            consumer2.Run();
        }
    }
}