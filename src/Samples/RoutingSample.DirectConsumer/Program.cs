using System;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Routers;

namespace RoutingSample.DirectConsumer
{
    internal class Program
    {
        public static IHorseRouteBus RouteBus;

        private static void Main(string[] args)
        {
            HorseClient client = new HorseClient();
            client.SetClientType("SAMPLE-MESSAGE-CONSUMER");

            DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(client.Direct);
            registrar.RegisterHandler<SampleDirectMessageMessageHandler>();

            client.MessageSerializer = new NewtonsoftContentSerializer();
            client.Connected += (c) => { Console.WriteLine("CONNECTED"); };
            client.Disconnected += (c) => Console.WriteLine("DISCONNECTED");
            client.MessageReceived += (client, message) => Console.WriteLine("Direct message received");

            client.Connect("horse://localhost:15500");

            while (true)
                Console.ReadLine();
        }
    }
}