using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Twino.Server;

namespace Benchmark.Json
{
    class Program
    {
        static void Main(string[] args)
        {
            ServerOptions options = new ServerOptions
                                    {
                                        RequestTimeout = 300000,
                                        HttpConnectionTimeMax = 300,
                                        MaximumHeaderLength = 8192,
                                        MaximumUriLength = 1024,
                                        MaximumRequestLength = 819200,
                                        MaximumPendingConnections = 0,
                                        PingInterval = 120000,
                                        Hosts = new List<HostOptions>
                                                {
                                                    new HostOptions
                                                    {
                                                        Port = 80
                                                    }
                                                }
                                    };

            TwinoServer server = TwinoServer.CreateHttp(async (twinoServer, request, response) =>
            {
                if (request.Path.Equals("/json", StringComparison.InvariantCultureIgnoreCase))
                    response.SetToJson(new {message = "Hello, World!"});
                else
                    response.StatusCode = HttpStatusCode.NotFound;

                await Task.CompletedTask;
            }, options);

            server.Start();
            server.BlockWhileRunning();
        }
    }
}