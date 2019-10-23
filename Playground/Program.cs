using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
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


    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 0; i < 25; i++)
                JsonSerializeTest();

            Console.WriteLine("done");
            Console.ReadLine();
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