using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Server.Http
{
    /// <summary>
    /// Handles all non-websocket Http Requests
    /// </summary>
    public interface IHttpRequestHandler
    {
        /// <summary>
        /// Triggered when a non-websocket request available.
        /// </summary>
        Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response);
    }
}