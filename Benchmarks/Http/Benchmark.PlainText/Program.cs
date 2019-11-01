using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Twino.Server;

namespace Benchmark.PlainText
{
    class Program
    {
        static void Main(string[] args)
        {
            TwinoServer server = TwinoServer.CreateHttp(async (twinoServer, request, response) =>
            {
                if (request.Path.Equals("/plaintext", StringComparison.InvariantCultureIgnoreCase))
                {
                    response.SetToText();
                    response.Write("Hello, World!");
                }
                else
                    response.StatusCode = HttpStatusCode.NotFound;

                await Task.CompletedTask;
            }, ServerOptions.CreateDefault());

            server.Start();
            server.BlockWhileRunning();
        }
    }
}