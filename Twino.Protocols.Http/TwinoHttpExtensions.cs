using Twino.Core;

namespace Twino.Protocols.Http
{
    public static class TwinoHttpExtensions
    {
        /// <summary>
        /// Uses HTTP Protocol and accepts HTTP connections
        /// </summary>
        public static ITwinoServer UseHttp(this ITwinoServer server, HttpRequestHandler action)
        {
            return UseHttp(server, action, HttpOptions.CreateDefault());
        }

        /// <summary>
        /// Uses HTTP Protocol and accepts HTTP connections
        /// </summary>
        public static ITwinoServer UseHttp(this ITwinoServer server, HttpRequestHandler action, HttpOptions options)
        {
            HttpMethodHandler handler = new HttpMethodHandler(action);
            TwinoHttpProtocol protocol = new TwinoHttpProtocol(server, handler, options);
            server.UseProtocol(protocol);
            return server;
        }
    }
}