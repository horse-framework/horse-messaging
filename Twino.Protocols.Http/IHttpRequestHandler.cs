using System.Threading.Tasks;

namespace Twino.Protocols.Http
{
    /// <summary>
    /// Handles all non-websocket Http Requests
    /// </summary>
    public interface IHttpRequestHandler
    {
        /// <summary>
        /// Triggered when a non-websocket request available.
        /// </summary>
        Task RequestAsync(HttpRequest request, HttpResponse response);
    }
}