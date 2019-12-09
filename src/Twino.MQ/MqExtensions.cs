using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.MQ
{
    /// <summary>
    /// Extension Methods for Twino.MQ
    /// </summary>
    public static class MqExtensions
    {
        /// <summary>
        /// Uses Twino.MQ Messaging Queue server
        /// </summary>
        public static ITwinoServer UseMqServer(this ITwinoServer server, MqServer mqServer)
        {
            MqConnectionHandler handler = new MqConnectionHandler(mqServer);
            server.UseTmq(handler);
            return server;
        }
    }
}