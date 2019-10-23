using System;
using System.Collections.Generic;
using Twino.Mvc;
using Twino.Server;

namespace Benchmark.Mvc.PlainText
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

            using TwinoMvc mvc = new TwinoMvc(options);
            mvc.Init();
            mvc.Run();
        }
    }
}