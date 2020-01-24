using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
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
            for (int i = 0; i < 250; i++)
            {
                Thread.Sleep(100);
                _ = T();
            }
            
            Console.ReadLine();
            Console.WriteLine(".");
            Console.ReadLine();
            Console.WriteLine(".");
            Console.ReadLine();
        }

        private static async Task T()
        {
            Console.Write(".");
            await Task.Delay(1000);
        }
    }
}