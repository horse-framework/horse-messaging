using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Benchmark.Cache
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("You should start Benchmark.Server to connect");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("This application connects with ONE client to the server.");
            Console.WriteLine("Sets string cache key with 'Hello, World' value once and gets the value n times.");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Press enter when you are ready");
            Console.ReadLine();

            HorseClient client = new HorseClient();
            client.Connected += c => Console.WriteLine("Connected to the server");
            await client.ConnectAsync("horse://localhost:27001");

            HorseResult result = await client.Cache.SetString("A", "Hello, World!");
            Console.WriteLine($"Set Cache Item: {result.Code}");

            Console.Write("Press enter to start to get and check the cache data");
            Console.ReadLine();
            string data = await client.Cache.GetString("A");
            Console.WriteLine($"Received: {data}");
            Console.WriteLine();

            while (true)
            {
                Console.Write("Press type count and press enter to start getting data from cache: ");
                int max = Convert.ToInt32(Console.ReadLine());

                Stopwatch sw = new Stopwatch();
                sw.Start();
                int count = 0;
                while (count < max)
                {
                    await client.Cache.GetString("A");
                    count++;
                }

                sw.Stop();
                Console.WriteLine($"Received {count} times in {sw.ElapsedMilliseconds} ms");
                Console.WriteLine();
            }
        }
    }
}