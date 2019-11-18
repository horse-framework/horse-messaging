using Twino.Core;

namespace Twino.Protocols.Http
{
    public static class TwinoHttpExtensions
    {
        public static ITwinoServer UseHttp(this ITwinoServer server, HttpRequestHandler action)
        {
            return UseHttp(server, action, HttpOptions.CreateDefault());
        }

        public static ITwinoServer UseHttp(this ITwinoServer server, HttpRequestHandler action, HttpOptions options)
        {
            HttpMethodHandler handler = new HttpMethodHandler(action);
            TwinoHttpProtocol protocol = new TwinoHttpProtocol(server, handler, options);
            server.UseProtocol(protocol);
            return server;
        }
    }
}