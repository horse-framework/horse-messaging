using System;
using System.Diagnostics;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
        }

        static void Measure(int count, Action method)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < count; i++)
                method();

            sw.Stop();
            Console.WriteLine("elapsed ms: " + sw.ElapsedMilliseconds);
        }
    }
}