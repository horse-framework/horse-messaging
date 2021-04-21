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

        static void Main(string[] args)
        {
            var tx = CountThread();
            
            for (int i = 0; i < 100; i++)
                _ = RunProducer("Test0");

            Console.ReadLine();
        }
        
        private static async Task RunProducer(string queue)
        {
            string x = new string('a', 1);
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(x));
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:27001");

            while (true)
            {
                ms.Position = 0;
                await client.Queue.Push(queue, ms, true);
                Interlocked.Increment(ref Count);
            }
        }
    }
}