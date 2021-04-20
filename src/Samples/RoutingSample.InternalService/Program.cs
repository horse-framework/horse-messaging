using System;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;

namespace RoutingSample.InternalService
{
    class Program
    {
        static void Main(string[] args)
        {
            HorseClient client = new HorseClient();
            client.SetClientName("GIVE-ME-GUID-REQUEST-HANDLER-CONSUMER");
            client.MessageSerializer = new NewtonsoftContentSerializer();

            DirectHandlerRegistrar registrar = new DirectHandlerRegistrar(client.Direct);
            registrar.RegisterHandler<GiveMeGuidRequestHandler>();

            client.Connected += (c) => { Console.WriteLine("CONNECTED"); };
            client.Disconnected += (c) => Console.WriteLine("DISCONNECTED");
            client.MessageReceived += (client, message) => Console.WriteLine("Direct message received");

            client.Connect("horse://localhost:15500");

            while (true)
                Console.ReadLine();
        }
    }
}