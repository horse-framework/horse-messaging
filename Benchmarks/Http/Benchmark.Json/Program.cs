using System;
using System.Net;
using System.Threading.Tasks;
using Twino.Server;

namespace Benchmark.Json
{
    class Program
    {
        static void Main(string[] args)
        {
            TwinoServer server = TwinoServer.CreateHttp(async (twinoServer, request, response) =>
            {
                if (request.Path.Equals("/json", StringComparison.InvariantCultureIgnoreCase))
                    response.SetToJson(new {message = "Hello, World!"});
                else
                    response.StatusCode = HttpStatusCode.NotFound;

                await Task.CompletedTask;
            }, ServerOptions.CreateDefault());

            server.Start();
            server.BlockWhileRunning();
        }
    }
}