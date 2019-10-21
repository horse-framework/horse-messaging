using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Server.Http
{
    /// <summary>
    /// HTTP request handler delegate for http requests
    /// </summary>
    public delegate void HttpRequestHandlerDelegate(TwinoServer server, HttpRequest request, HttpResponse response);

    /// <summary>
    /// Created for http request handling implementation of hgttp server.
    /// When developer does not want to create new handler class and want to handle the requests via methods.
    /// This factory will be created and points the method delegate
    /// </summary>
    public class MethodHttpRequestHandler : IHttpRequestHandler
    {
        private readonly HttpRequestHandlerDelegate _handler;

        public MethodHttpRequestHandler(HttpRequestHandlerDelegate handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Triggered when a non-websocket request available.
        /// </summary>
        public void Request(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            _handler(server, request, response);
        }

        /// <summary>
        /// Triggered when a non-websocket request available.
        /// </summary>
        public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            await Task.Factory.StartNew(() => Request(server, request, response));
        }
        
    }
}