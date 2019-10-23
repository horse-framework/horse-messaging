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
            ServerOptions options = new ServerOptions
                                    {
                                        RequestTimeout = 300000,
                                        HttpConnectionTimeMax = 30,
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
                if (request.Path.Equals("/plaintext", StringComparison.InvariantCultureIgnoreCase))
                {
                    response.SetToText();
                    response.Write("Hello, World!");
                }
                else
                    response.StatusCode = HttpStatusCode.NotFound;

                await Task.CompletedTask;
            }, options);

            server.Start();
            server.BlockWhileRunning();
        }
    }
}