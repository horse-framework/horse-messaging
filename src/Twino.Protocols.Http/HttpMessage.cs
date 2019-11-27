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

        public HttpMessage()
        {
        }

        public HttpMessage(HttpRequest request, HttpResponse response)
        {
            Request = request;
            Response = response;
        }
    }
}