using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Benchmark.Helper;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Benchmark.Channel.Publisher;

class Program
{
    private static Counter _counter;

    private static long total;
    private static bool stop;

    static async Task Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("You should start Benchmark.Server to connect");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("This application connects with n clients to the server.");
        Console.WriteLine("Each client sends messages to the same channel in a loop.");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("Press enter when you are ready");
        Console.ReadLine();

        _counter = new Counter();
        _counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));

        HorseClient client = new HorseClient();
        client.SetClientName("publisher");
        client.SetClientType("publisher");
        await client.ConnectAsync("horse://localhost:2626");

        ChannelModel model = new ChannelModel { Foo = "123" };

        HorseResult result = await client.Channel.Publish("channel", model, false);
        Console.WriteLine($"First publish result: {result.Code}");
        Console.Write("Enter client count (1-100): ");
        int count = Math.Max(1, Math.Min(100, Convert.ToInt32(Console.ReadLine())));
        Console.Write("Wait of acknowledge? (Y/N): ");
        bool ack = Console.ReadLine().Trim().ToUpper() == "Y";

        total = 0;

        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < 3; j++)
                _ = Run(ack, "channel-" + (i + 1));
        }

        Console.ReadLine();
    }


    private static async Task Run(bool waitForAck, string name)
    {
        ChannelModel model = new ChannelModel { Foo = "123" };
        HorseClient client = new HorseClient();
        client.SetClientName("publisher");
        client.SetClientType("publisher");
        int i = 0;
        await client.ConnectAsync("horse://localhost:2626");
        string demoString = new string('a', 10000);

        while (!stop)
        {
            HorseResult result;
            if (i == 400)
            {
                i = 0;
                result = await client.Channel.PublishString(name, demoString, waitForAck);
            }
            else
            {
                i++;
                await client.Channel.PublishString(name, demoString, waitForAck);
            }

            Interlocked.Increment(ref total);
            _counter.Increase();
        }
    }
}