using System;
using Benchmark.Helper;
using Horse.Messaging.Client;

namespace Benchmark.Queue.Consumer;

class Program
{
    private static Counter _counter;

    static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("You should start Benchmark.Server to connect.");
        Console.WriteLine("The queue may be empty.");
        Console.WriteLine("You can run Benchmark.Queue.Producer application to fill the queue.");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("This application connects with ONE client to the server.");
        Console.WriteLine("Subscribes to a queue and consumes message from the queue.");
        Console.WriteLine("It sends ack to the server for each consumed message.");

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("Enter client count (1-100): ");
        int count = Math.Max(1, Math.Min(100, Convert.ToInt32(Console.ReadLine())));

        Console.WriteLine("Press enter when you are ready");
        Console.ReadLine();

        _counter = new Counter();
        _counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));

        for (int i = 0; i < count; i++)
            StartClient();

        while (true)
            Console.ReadLine();
    }

    private static void StartClient()
    {
        HorseClient client = new HorseClient();
        client.MessageReceived += (c, m) => _counter.Increase();
        client.SetClientName("Benchmark.Queue.Consumer");
        client.SetClientType("Benchmark");
        client.AutoAcknowledge = true;
        client.Connect("horse://localhost:2626");
        _ = client.Queue.Subscribe("TestQueue", true);
    }
}