using Twino.Core;
using Twino.Protocols.Http;

namespace Twino.Mvc
{
    /// <summary>
    /// Extension methods for Twino MVC
    /// </summary>
    public static class MvcExtensions
    {
        /// <summary>
        /// Uses HTTP Protocol and accepts HTTP connections with Twino MVC Architecture
        /// </summary>
        public static ITwinoServer UseMvc(this ITwinoServer server, TwinoMvc mvc, string optionsFilename)
        {
            return UseMvc(server, mvc, HttpOptions.Load(optionsFilename));
        }
        
        /// <summary>
        /// Uses HTTP Protocol and accepts HTTP connections with Twino MVC Architecture
        /// </summary>
        public static ITwinoServer UseMvc(this ITwinoServer server, TwinoMvc mvc)
        {
            return UseMvc(server, mvc, HttpOptions.CreateDefault());
        }

        /// <summary>
        /// Uses HTTP Protocol and accepts HTTP connections with Twino MVC Architecture
        /// </summary>
        public static ITwinoServer UseMvc(this ITwinoServer server, TwinoMvc mvc, HttpOptions options)
        {
            MvcConnectionHandler handler = new MvcConnectionHandler(mvc, mvc.AppBuilder);
            TwinoHttpProtocol protocol = new TwinoHttpProtocol(server, handler, options);
            server.UseProtocol(protocol);
            return server;
        }
    }
}