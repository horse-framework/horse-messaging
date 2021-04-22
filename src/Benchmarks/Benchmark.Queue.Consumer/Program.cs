using System;
using Benchmark.Channel.Publisher;
using Horse.Messaging.Client;

namespace Benchmark.Queue.Consumer
{
    class Program
    {
        private static Counter _counter;
        static void Main(string[] args)
        {
            _counter = new Counter();
            _counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));
            
            HorseClient client = new HorseClient();
            client.MessageReceived += (c, m) => _counter.Increase();
            client.AutoAcknowledge = true;
            client.Connected += c => Console.WriteLine("connected");
            client.Disconnected += c => Console.WriteLine("disconnected");
            client.Connect("horse://localhost:27001");
            client.Queue.Subscribe("Test0", true);

            Console.ReadLine();
            client.Disconnect();
            Console.ReadLine();
        }
    }
}