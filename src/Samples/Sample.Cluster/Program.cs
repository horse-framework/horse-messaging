using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Handlers;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues.Delivery;
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
            HorseRider rider = HorseRiderBuilder.Create()
               .ConfigureQueues(q => q.UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No))
               .Build();

            rider.NodeManager.SetHost(new HostOptions {Port = 26101});
            rider.NodeManager.AddRemoteNode(new NodeOptions
            {
                Host = "horse://localhost:26100",
                Name = "Node-2",
                ReconnectWait = 500
            });

            HorseServer server = new HorseServer();
            server.UseRider(rider);
            server.Start(26001);
            return rider;
        }

        static HorseRider StartServer2()
        {
            HorseRider rider = HorseRiderBuilder.Create()
               .ConfigureQueues(q => q.UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No))
               .Build();

            rider.NodeManager.SetHost(new HostOptions {Port = 26100});
            rider.NodeManager.AddRemoteNode(new NodeOptions
            {
                Host = "horse://localhost:26101",
                Name = "Node-1",
                ReconnectWait = 500
            });

            HorseServer server = new HorseServer();
            server.UseRider(rider);
            server.Start(26002);
            return rider;
        }

        static void ConnectToServer1AsProducer()
        {
            HorseClient client = new HorseClient();
            client.MessageSerializer = new NewtonsoftContentSerializer();
            client.Connect("horse://localhost:26002");

            Task.Run(async () =>
            {
                int counter = 1;
                while (true)
                {
                    await Task.Delay(1000);

                    MirroredModel model = new MirroredModel();
                    model.Foo = $"Foo #{counter}";

                    HorseResult pushResult = await client.Queue.PushJson(model, true);
                    Console.WriteLine($"Push Result: {pushResult.Code}");

                    counter++;
                }
            });
        }

        static void ConnectToServer2AsConsumer()
        {
            //that client is connected to same node with producer
            HorseClient client1 = new HorseClient();
            client1.MessageSerializer = new NewtonsoftContentSerializer();
            QueueConsumerRegistrar registrar1 = new QueueConsumerRegistrar(client1.Queue);
            registrar1.RegisterConsumer<MirroredModelConsumer>();
            client1.Connect("horse://localhost:26002");

            //that client is connected to other node
            HorseClient client2 = new HorseClient();
            client2.MessageSerializer = new NewtonsoftContentSerializer();
            QueueConsumerRegistrar registrar2 = new QueueConsumerRegistrar(client2.Queue);
            registrar2.RegisterConsumer<MirroredModelConsumer>();
            client2.Connect("horse://localhost:26001");
        }
    }
}