using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Server;
using Twino.Server.Http;

namespace Sample.Full.Server
{
    public class RequestHandler : IHttpRequestHandler
    {
        public void Request(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            response.SetToHtml();
            response.Write("Hello World!");
        }

        public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            await Task.Factory.StartNew(() => Request(server, request, response));
        }
    }
}
