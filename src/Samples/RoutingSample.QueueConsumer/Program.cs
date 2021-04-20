using System;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;

namespace RoutingSample.QueueConsumer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            HorseClient client = new HorseClient();
            client.RemoteHost = "horse://localhost:15500";
            client.MessageSerializer = new NewtonsoftContentSerializer();

            QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(client.Queue);
            registrar.RegisterConsumer<SampleMessageQueueConsumer>();

            client.Connected += (c) =>
            {
                Console.WriteLine("CONNECTED");
                _ = client.Queue.Subscribe("SAMPLE-MESSAGE-QUEUE", false);
            };

            client.Disconnected += (c) => Console.WriteLine("DISCONNECTED");
            client.MessageReceived += (c, message) => Console.WriteLine("Queue message received");

            client.Connect();

            while (true)
                Console.ReadLine();
        }
    }
}