using System;
using Twino.Server;
using Twino.Server.Http;
using Twino.Server.WebSockets;

namespace Sample.Full.Server
{
    class CustomException : Exception
    {
        public CustomException(string message) : base(message) { }
    }

    class Program
    {
        static void Main(string[] args)
        {
            IClientFactory factory = new ClientFactory();
            IHttpRequestHandler handler = new RequestHandler();
            IClientContainer container = new ClientContainer();

            ServerOptions options = new ServerOptions
            {
                MaximumPendingConnections = 48,
                MaximumRequestLength = 2048,
                PingInterval = 60000
            };

            TwinoServer server = new TwinoServer(handler, factory, container, options);

            server.Start(8080);
            // Remove comment signs for exception test
            // throw new CustomException("Simple exception");
        }
    }
}
