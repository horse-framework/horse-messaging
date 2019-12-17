using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace Playground
{
    class Program
    {
        private static volatile int X = 0;

        static void Main(string[] args)
        {
            int max = 28000000;
            Thread thread = new Thread(async () =>
            {
                while (true)
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    for (int i = 0; i < max; i++)
                        await A();

                    SpinWait wait = new SpinWait();
                    while (X < max)
                        wait.SpinOnce();

                    sw.Stop();
                    Console.WriteLine(sw.ElapsedMilliseconds);
                    Console.ReadLine();
                    X = 0;
                }
            });

            thread.Start();

            while (true)
                Thread.Sleep(5000);
        }

        private static async Task<bool> A()
        {
            X++;
            return await Task.FromResult(true);
        }
    }
}