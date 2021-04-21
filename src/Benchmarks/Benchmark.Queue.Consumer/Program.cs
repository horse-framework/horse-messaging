using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;

namespace Benchmark.Queue.Consumer
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

            HorseClient client = new HorseClient();
            client.MessageReceived += (c, m) => Interlocked.Increment(ref Count);
            client.Connected += c => Console.WriteLine("connected"); 
            client.Disconnected += c => Console.WriteLine("disconnected"); 
            client.Connect("horse://localhost:27001");
            client.Queue.Subscribe("Test0", true);

            Console.ReadLine();
        }
    }
}