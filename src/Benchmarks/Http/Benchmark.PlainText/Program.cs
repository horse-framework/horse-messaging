using System;
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
            server.UseHttp((request, response) =>
            {
                if (request.Path.Equals("/plaintext", StringComparison.InvariantCultureIgnoreCase))
                {
                    response.SetToText();
                    return response.WriteAsync("Hello, World!");
                }

                response.StatusCode = HttpStatusCode.NotFound;
                return Task.CompletedTask;
                
            }, HttpOptions.CreateDefault());

            server.Start(80);
            server.BlockWhileRunning();
        }
    }
}