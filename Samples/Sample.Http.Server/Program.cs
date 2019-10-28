using Twino.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Http.Server
{
    class Program
    {
        private static byte[] HELLO_WORLD = Encoding.UTF8.GetBytes("Hello, World!");
        private static Timer _timer;
        public static List<int> RpsList = new List<int>();
        public static volatile int Rps;
        public static int Total;

        static void Main(string[] args)
        {
            _timer = new Timer(s =>
            {
                int r = Rps;
                Rps -= r;
                if (r < 2)
                    return;

                RpsList.Add(r);
                int avg = Convert.ToInt32(RpsList.Average(x => x));
                Console.WriteLine($"Rps: {r}, Avg: {avg}, Total: {Total}");
            }, null, 1000, 1000);


            TwinoServer server = TwinoServer.CreateHttp(async (twinoServer, request, response) =>
            {
                if (request.Path.Equals("/plaintext", StringComparison.InvariantCultureIgnoreCase))
                {
                    response.SetToText();
                    Rps++;
                    await response.WriteAsync("Hello, World!");
                }
                else
                    response.StatusCode = HttpStatusCode.NotFound;

            }, ServerOptions.CreateDefault());

            server.Options.ContentEncoding = null;
            server.Options.Hosts[0].Port = 82;
            
            server.Start();
            server.BlockWhileRunning();
        }
    }
}