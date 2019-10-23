using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Server;
using Twino.Server.Http;

namespace Test.Server.Helpers
{
    public class HttpHandler : IHttpRequestHandler
    {
        public void Request(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            var model = new
                        {
                            Success = true,
                            Message = "Hello world"
                        };
            
            response.SetToJson(model);
        }

        public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            await Task.Factory.StartNew(() => Request(server, request, response));
        }
    }
}