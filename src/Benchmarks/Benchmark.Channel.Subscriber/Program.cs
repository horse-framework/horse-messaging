using System;
using System.Threading;
using System.Threading.Tasks;
using Benchmark.Helper;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Protocol;

namespace Benchmark.Channel.Subscriber;

class Program
{
    public static Counter Counter { get; private set; }

    private static long total;

    static async Task Main(string[] args)
    {
        Counter = new Counter();
        Counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));


        HorseClient client = new HorseClient();
        client.AddHost("horse://127.0.0.1:2626");
        client.SetClientName("Test");
        client.SetClientType("Test");

        client.MessageReceived += (c, m) =>
        {
            Counter.Increase();
            Interlocked.Increment(ref total);
        };

        await client.ConnectAsync();


        Console.Write("Channel Count: ");
        int count = Convert.ToInt32(Console.ReadLine());
        for (int i = 1; i <= count; i++)
        {
            string name = "channel-" + i;
            HorseResult result = await client.Channel.Subscribe(name, true);
            Console.WriteLine($"Subscription to {name} result: {result.Code}");
        }

        while (true)
        {
            Console.ReadLine();
            Console.WriteLine("Total: " + total);
        }
    }
}