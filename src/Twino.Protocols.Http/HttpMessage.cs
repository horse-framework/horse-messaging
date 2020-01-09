namespace Twino.Protocols.Http
{
    /// <summary>
    /// Http Protocol request and response messages
    /// </summary>
    public class HttpMessage
    {
        /// <summary>
        /// Request message model from client
        /// </summary>
        public HttpRequest Request { get; set; }

        /// <summary>
        /// Response model that will be written to network stream as response
        /// </summary>
        public HttpResponse Response { get; set; }

        /// <summary>
        /// Creates new empty HTTP Message
        /// </summary>
        public HttpMessage()
        {
        }

        /// <summary>
        /// Creates new HTTP Message with request and response
        /// </summary>
        public HttpMessage(HttpRequest request, HttpResponse response)
        {
            Request = request;
            Response = response;
        }
    }
}