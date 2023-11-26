using System;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Sample.DirectHandler;

class Program
{
    static void Main(string[] args)
    {
        HorseClientBuilder builder = new HorseClientBuilder();

        HorseClient client = builder.SetHost("horse://localhost:9999")
            .SetClientType("direct-handler")
            .OnMessageReceived(MessageReceived)
            .OnConnected(Connected)
            .AddSingletonDirectHandlers(typeof(Program))
            .Build();

        client.Connect();

        while (true)
            Console.ReadLine();
    }

    private static void Connected(HorseClient client)
    {
        Console.WriteLine($"[connected]");

    }

    private static void MessageReceived(HorseMessage message)
    {
        Console.WriteLine($"[received] ${message}");
    }
}