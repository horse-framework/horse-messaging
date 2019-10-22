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
        public void Request(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            response.SetToHtml();
            response.StatusCode = HttpStatusCode.OK;
            response.Write("Hello world");
            //Console.WriteLine("Hello World: " + request.Content);
        }

        public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            await Task.Factory.StartNew(() => Request(server, request, response));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            RequestHandler handler = new RequestHandler();
            TwinoServer server = TwinoServer.CreateHttp(handler);
            server.Start();

            Console.ReadLine();
        }
    }
}