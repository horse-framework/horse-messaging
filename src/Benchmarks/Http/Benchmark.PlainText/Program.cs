using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Twino.Protocols.Http;
using Twino.Server;

namespace Benchmark.PlainText
{
    class Program
    {
        static void Main(string[] args)
        {
            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
            server.UseHttp(async (request, response) =>
            {
                if (request.Path.Equals("/plaintext", StringComparison.InvariantCultureIgnoreCase))
                {
                    response.SetToText();
                    response.Write("Hello, World!");
                }
                else
                    response.StatusCode = HttpStatusCode.NotFound;

                await Task.CompletedTask;
            }, HttpOptions.CreateDefault());

            server.Start(5000);
            server.BlockWhileRunning();
        }
    }
}