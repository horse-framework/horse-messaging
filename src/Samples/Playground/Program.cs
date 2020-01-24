using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Xunit.Sdk;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            List<ThreadTimer> timers = new List<ThreadTimer>();
            for (int i = 0; i < 17; i++)
            {
                ThreadTimer timer = new ThreadTimer(A, TimeSpan.FromMilliseconds(1000));
                timer.WaitTickCompletion = false;
                timer.Start();
                timers.Add(timer);
            }

            while (true)
            {
                Console.Write(".");
                Thread.Sleep(50);
            }
        }

        private static void A()
        {
            Thread.Sleep(5000);
            Console.Write("*");
        }
    }
}