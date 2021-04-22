using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Benchmark.Channel.Publisher;
using Horse.Messaging.Client;

namespace Benchmark.Queue.Producer
{
    class Program
    {
        private static Counter _counter;
        private static HorseClient client;

        static void Main(string[] args)
        {
            _counter = new Counter();
            _counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));

            for (int i = 0; i < 1; i++)
                _ = RunProducer("Test0");

            Console.ReadLine();
            client.Disconnect();
            Console.ReadLine();
        }

        private static async Task RunProducer(string queue)
        {
            string x = new string('a', 1);
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(x));
            client = new HorseClient();
            client.Connected += c => Console.WriteLine("connected");
            client.Disconnected += c => Console.WriteLine("disconnected");

            await client.ConnectAsync("horse://localhost:27001");

            try
            {
                int y = 0;
                while (client.IsConnected)
                {
                    ms.Position = 0;
                    y++;
                    await client.Queue.Push(queue, ms, false);
                    _counter.Increase();
                    if (y == 10)
                    {
                        await Task.Delay(1);
                        y = 0;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}