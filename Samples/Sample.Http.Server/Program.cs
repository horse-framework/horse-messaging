using Twino.Server;
using System;
using System.Net;

namespace Sample.Http.Server
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
                    await response.WriteAsync("Hello, World!");
                }
                else
                    response.StatusCode = HttpStatusCode.NotFound;
            }, ServerOptions.CreateDefault());

            server.Options.ContentEncoding = null;

            server.Start();
            server.BlockWhileRunning();
        }
    }
}