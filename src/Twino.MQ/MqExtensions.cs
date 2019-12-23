using Twino.MQ.Network;
using Twino.Protocols.TMQ;
using Twino.Server;

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
        public static TwinoServer UseMqServer(this TwinoServer server, MqServer mqServer)
        {
            MqConnectionHandler handler = new MqConnectionHandler(mqServer);
            mqServer.Server = server;
            server.UseTmq(handler);
            return server;
        }
    }
}