using System;
using Benchmark.Helper;
using Horse.Messaging.Client;

namespace Benchmark.Queue.Consumer
{
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
            Console.WriteLine("Press enter when you are ready");
            Console.ReadLine();

            _counter = new Counter();
            _counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));

            HorseClient client = new HorseClient();
            client.MessageReceived += (c, m) => _counter.Increase();
            client.AutoAcknowledge = true;
            client.Connect("horse://localhost:27001");
            client.Queue.Subscribe("TestQueue", true);

            Console.ReadLine();
            client.Disconnect();
            Console.ReadLine();
        }
    }
}