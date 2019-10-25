using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Mvc;
using Twino.Mvc.Auth;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using Twino.Server;
using Twino.Server.Http;
using Twino.Server.WebSockets;
using Twino.SocketModels;

namespace Playground
{
    public class Leaf
    {
        public int Count { get; set; }
        public string Color { get; set; }
    }

    public class Foo
    {
        public string Foo1 { get; set; } = "foo";
        public string Foo2 { get; set; } = "foo";
        public string Foo3 { get; set; } = "foo";
        public string Foo4 { get; set; } = "foo";
        public string Foo5 { get; set; } = "foo";

        public List<Leaf> Leaves { get; set; } = new List<Leaf>
                                                 {
                                                     new Leaf {Color = "Blue", Count = 12},
                                                     new Leaf {Color = "Blue", Count = 12},
                                                     new Leaf {Color = "Blue", Count = 12},
                                                     new Leaf {Color = "Blue", Count = 12},
                                                     new Leaf {Color = "Blue", Count = 12},
                                                     new Leaf {Color = "Blue", Count = 12}
                                                 };
    }

    public class HW1
    {
        public string Message { get; set; }
    }

    public class HW2
    {
        public string Message;
    }

    public struct HW3
    {
        public string Message;

        public HW3(string msg)
        {
            Message = msg;
        }
    }

    class Program
    {
        static void Measure(int count, Action method)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < count; i++)
                method();

            sw.Stop();
            Console.WriteLine("elapsed ms: " + sw.ElapsedMilliseconds);
            Console.ReadLine();
        }

        static void Main(string[] args)
        {
            
        }

        static void Tasks()
        {
            while (true)
            {
                Stopwatch sw1 = new Stopwatch();
                sw1.Start();
                for (int i = 0; i < 2000000; i++)
                    W1();

                sw1.Stop();
                Console.WriteLine("sw1: " + sw1.ElapsedMilliseconds);
                Console.ReadLine();

                Stopwatch sw2 = new Stopwatch();
                TaskCompletionSource<bool> s1 = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(async (c) =>
                {
                    sw2.Start();
                    for (int i = 0; i < 2000000; i++)
                        await W2();
                    sw2.Stop();
                    s1.SetResult(true);
                });
                s1.Task.Wait();
                Console.WriteLine("sw2: " + sw2.ElapsedMilliseconds);
                Console.ReadLine();

                Stopwatch sw3 = new Stopwatch();
                TaskCompletionSource<bool> s2 = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(async (c) =>
                {
                    sw3.Start();
                    for (int i = 0; i < 2000000; i++)
                        await W3();
                    sw3.Stop();
                    s2.SetResult(true);
                });
                s2.Task.Wait();
                Console.WriteLine("sw3: " + sw3.ElapsedMilliseconds);
                Console.ReadLine();
            }
        }

        static int W1()
        {
            return Convert.ToInt32(Math.Pow(4, 5));
        }

        static async Task<int> W2()
        {
            return Convert.ToInt32(Math.Pow(4, 5));
        }

        static async Task<int> W3()
        {
            return await Task.FromResult(Convert.ToInt32(Math.Pow(4, 5)));
        }

        static void JsonSerializeTest()
        {
            int bytes = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int j = 0; j < 100000; j++)
            {
                string s = System.Text.Json.JsonSerializer.Serialize(new Foo());
                byte[] arr = Encoding.UTF8.GetBytes(s);
                //byte[] arr = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new Foo());
                bytes += arr.Length;
            }

            sw.Stop();
            Console.WriteLine(bytes + " bytes");
            Console.WriteLine($"completed in {sw.ElapsedMilliseconds} ms");
            Console.ReadLine();
        }

        static void StreamWriterTest()
        {
            long bytes = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int j = 0; j < 1000; j++)
            {
                MemoryStream ms = new MemoryStream();
                StreamWriter writer = new StreamWriter(ms, Encoding.UTF8);
                for (int i = 0; i < 1000; i++)
                    writer.WriteLine(new string('A', 10) + ":" + new string('B', 20));

                writer.Flush();
                byte[] array = ms.ToArray();
                bytes += array.Length;
            }

            sw.Stop();
            Console.WriteLine(bytes + " bytes");
            Console.WriteLine($"completed in {sw.ElapsedMilliseconds} ms");
            Console.ReadLine();
        }


        static void StringBuilderTest()
        {
            long bytes = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int j = 0; j < 1000; j++)
            {
                StringBuilder builder = new StringBuilder();

                for (int i = 0; i < 1000; i++)
                    builder.AppendLine(new string('A', 10) + ":" + new string('B', 20));

                byte[] array = Encoding.UTF8.GetBytes(builder.ToString());
                bytes += array.Length;
            }

            sw.Stop();
            Console.WriteLine(bytes + " bytes");
            Console.WriteLine($"completed in {sw.ElapsedMilliseconds} ms");
            Console.ReadLine();
        }
    }
}