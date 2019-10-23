using Twino.Server;
using Twino.Server.Http;
using System;
using System.Net;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Sample.Http.Server
{
    public class RequestHandler : IHttpRequestHandler
    {
        public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            response.SetToHtml();
            response.Write("Hello World!");
            Console.WriteLine("Hello World: " + request.Content);
            await Task.CompletedTask;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            RequestHandler handler = new RequestHandler();
            TwinoServer server = TwinoServer.CreateHttp(handler);
            server.Start(82);
            server.BlockWhileRunning();
        }
    }
}