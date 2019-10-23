using System;
using System.Diagnostics;
using System.Net.Sockets;
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
    class Program
    {
        static void Main(string[] args)
        {
            Console.ReadLine();
            Stopwatch sw = new Stopwatch();
            string s1 = DateTime.UtcNow.ToString("R");
            string s = DateTime.UtcNow.ToString("R");
            sw.Start();

            for (int i = 0; i < 1000000; i++)
                s = s1.Trim();

            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.WriteLine(s);
            Console.ReadLine();
        }
    }
}