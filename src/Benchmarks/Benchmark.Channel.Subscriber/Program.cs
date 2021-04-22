using System;
using System.Threading.Tasks;
using Benchmark.Channel.Publisher;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Benchmark.Channel.Subscriber
{
    class Program
    {
        public static Counter Counter { get; private set; }

        static async Task Main(string[] args)
        {
            Counter = new Counter();
            Counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:27001");

            HorseResult result = await client.Channel.Subscribe("ch1", true);
            Console.WriteLine($"Subscription result: {result.Code}");
            Console.ReadLine();
        }
    }
}