using Twino.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Http.Server
{
    class Program
    {
        private static Timer _timer;
        public static List<int> RpsList = new List<int>();
        public static int Rps { get; set; }

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
                Console.WriteLine($"Rps: {r}, Avg: {avg}");
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