using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Benchmark.Channel.Publisher
{
    class Program
    {
        private static Counter _counter;

        static async Task Main(string[] args)
        {
            _counter = new Counter();
            _counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:27001");

            HorseResult result = await client.Channel.Publish("ch1", "Hello, world!", true);
            Console.WriteLine($"First publish result: {result.Code}");
            Console.ReadLine();

            for (int i = 0; i < 50; i++)
                _ = Run();

            while (true)
            {
                await client.Channel.Publish("ch1", "Hello, world!", true);
                _counter.Increase();
            }
        }

        private static async Task Run()
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:27001");
            while (true)
            {
                await client.Channel.Publish("ch1", "Hello, world!", true);
                _counter.Increase();
            }
        }
    }
}