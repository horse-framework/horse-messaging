using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Benchmark.Cache;

class Program
{
    static async Task Main(string[] args)
    {
        string hostname = "horse://localhost:27001";

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
        await client.ConnectAsync(hostname);

        HorseResult result = await client.Cache.SetString("A", "Hello, World!", TimeSpan.FromMinutes(30));
        Console.WriteLine($"Set Cache Item: {result.Code}");

        Console.Write("Press enter to start to get and check the cache data");
        Console.ReadLine();
        var cacheData = await client.Cache.GetString("A");
        Console.WriteLine($"Received: {cacheData.Value}");
        Console.WriteLine();

        List<HorseClient> clients = new List<HorseClient>();

        Console.Write("Press type how many concurrent clients: ");
        int clientCount = Convert.ToInt32(Console.ReadLine());

        for (int i = 0; i < clientCount; i++)
        {
            HorseClient c = new HorseClient();
            await c.ConnectAsync(hostname);
            clients.Add(c);
        }

        while (true)
        {
            Console.Write("Press type count and press enter to start getting data from cache: ");
            int max = Convert.ToInt32(Console.ReadLine());

            List<Task> clientTasks = new List<Task>();

            Stopwatch sw = new Stopwatch();

            bool start = false;
                
            foreach (HorseClient c in clients)
            {
                clientTasks.Add(Task.Run(async () =>
                {
                    while (!start)
                        await Task.Delay(1);
                        
                    for (int j = 0; j < max; j++)
                        await c.Cache.GetString("A");
                }));
            }

            start = true;
            await Task.Delay(1);
                
            sw.Start();
            Task.WaitAll(clientTasks.ToArray());
            sw.Stop();

            Console.WriteLine($"Received {clientCount * max} times in {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }
    }
}