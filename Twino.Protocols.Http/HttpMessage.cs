namespace Twino.Protocols.Http
{
    public class HttpMessage
    {
        public HttpRequest Request { get; set; }
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