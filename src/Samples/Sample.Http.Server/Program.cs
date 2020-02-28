using System;
using System.Net;
using Twino.Protocols.Http;
using Twino.Server;

namespace Sample.Http.Server
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
                    await response.WriteAsync("Hello, World!");
                }
                else
                    response.StatusCode = HttpStatusCode.NotFound;
            });

            server.Start(82);
            server.BlockWhileRunning();
        }
    }
}