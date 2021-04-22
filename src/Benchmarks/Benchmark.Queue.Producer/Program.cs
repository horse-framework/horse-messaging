using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;

namespace Benchmark.Queue.Producer
{
    class Program
    {
        private static long Count;
        private static HorseClient client;

        private static Thread CountThread()
        {
            int seconds = 0;
            Thread thread = new Thread(async () =>
            {
                while (true)
                {
                    long prevPush = Count;
                    await Task.Delay(1000);
                    long curPush = Count;
                    long difPush = curPush - prevPush;

                    seconds++;
                    Console.WriteLine($"{difPush} m/s \t {curPush} total \t {seconds} secs");
                }
            });
            thread.Start();
            return thread;
        }

        static async Task Main(string[] args)
        {
            var tx = CountThread();
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
                    if (y == 10)
                    {
                        await Task.Delay(1);
                        y = 0;
                    }

                    Interlocked.Increment(ref Count);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}