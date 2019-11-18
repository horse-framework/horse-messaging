using Twino.Core;
using Twino.Mvc.Middlewares;
using Twino.Protocols.Http;

namespace Twino.Mvc
{
    public static class MvcExtensions
    {
        public static ITwinoServer UseMvc(this ITwinoServer server, TwinoMvc mvc, HttpOptions options)
        {
            MvcAppBuilder builder = new MvcAppBuilder(mvc);
            MvcConnectionHandler handler = new MvcConnectionHandler(mvc, builder);
            TwinoHttpProtocol protocol = new TwinoHttpProtocol(server, handler, options);
            server.UseProtocol(protocol);
            return server;
        }
    }
}