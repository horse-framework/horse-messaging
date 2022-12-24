using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Benchmark.Helper;
using Horse.Messaging.Client;

namespace Benchmark.Queue.Producer
{
    class Program
    {
        private static Counter _counter;
        private static HorseClient client;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("You should start Benchmark.Server to connect.");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("This application connects with n clients to the server.");
            Console.WriteLine("Each client pushes a message to the same queue.");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Press enter when you are ready");
            Console.ReadLine();

            Console.Write("Enter client count (1-100): ");
            int count = Math.Max(1, Math.Min(100, Convert.ToInt32(Console.ReadLine())));
            Console.Write("Wait of acknowledge? (Y/N): ");
            bool ack = Console.ReadLine().Trim().ToUpper() == "Y";

            _counter = new Counter();
            _counter.Run(c => Console.WriteLine($"{c.ChangeInSecond} m/s \t {c.Total} total \t"));

            for (int i = 0; i < count; i++)
                _ = RunProducer("TestQueue", ack);

            Console.ReadLine();
            client.Disconnect();
            Console.ReadLine();
        }

        private static async Task RunProducer(string queue, bool waitForAck)
        {
            string x = new string('a', 1);
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(x));
            client = new HorseClient();
            client.Queue.SetOptions("fsd", o =>
            {

            });

            await client.ConnectAsync("horse://localhost:27001");

            try
            {
                while (client.IsConnected)
                {
                    ms.Position = 0;
                    await client.Queue.Push(queue, ms, waitForAck);
                    _counter.Increase();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}