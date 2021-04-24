using System;
using System.Threading.Tasks;
using Benchmark.Helper;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Channels;
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
            client.Channel.NameHandler = c =>
            {
                return "channel";
            };
            ChannelConsumerRegistrar registrar = new ChannelConsumerRegistrar(client.Channel);
            registrar.RegisterHandler<ChannelSubscriber>();
            await client.ConnectAsync("horse://localhost:27001");

            HorseResult result = await client.Channel.Subscribe("channel", true);
            Console.WriteLine($"Subscription result: {result.Code}");
            Console.ReadLine();
        }
    }
}