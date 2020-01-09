using Twino.Core;

namespace Twino.Protocols.Http
{
    /// <summary>
    /// Extention methods for Twino HTTP protocol
    /// </summary>
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
        /// <summary>
        /// Uses HTTP Protocol and accepts HTTP connections
        /// </summary>
        public static ITwinoServer UseHttp(this ITwinoServer server, HttpRequestHandler action, string optionsFilename)
        {
            HttpMethodHandler handler = new HttpMethodHandler(action);
            TwinoHttpProtocol protocol = new TwinoHttpProtocol(server, handler, HttpOptions.Load(optionsFilename));
            server.UseProtocol(protocol);
            return server;
        }
    }
}