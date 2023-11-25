using System;
using System.Threading.Tasks;
using Benchmark.Helper;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Benchmark.Channel.Publisher;

class Program
{
    private static Counter _counter;

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
        await client.ConnectAsync("horse://localhost:27001");

        ChannelModel model = new ChannelModel {Foo = "123"};

        HorseResult result = await client.Channel.Publish("channel", model, true);
        Console.WriteLine($"First publish result: {result.Code}");
        Console.Write("Enter client count (1-100): ");
        int count = Math.Max(1, Math.Min(100, Convert.ToInt32(Console.ReadLine())));
        Console.Write("Wait of acknowledge? (Y/N): ");
        bool ack = Console.ReadLine().Trim().ToUpper() == "Y";

        for (int i = 0; i < count; i++)
            _ = Run(ack);

        Console.ReadLine();
    }

    private static async Task Run(bool waitForAck)
    {
        ChannelModel model = new ChannelModel {Foo = "123"};
        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:27001");
        while (true)
        {
            await client.Channel.Publish("channel", model, waitForAck);
            _counter.Increase();
        }
    }
}