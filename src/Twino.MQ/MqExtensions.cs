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

            mqServer.NodeServer.ConnectionHandler = new NodeConnectionHandler(mqServer.NodeServer, handler);
            server.UseTmq(handler);

            if (mqServer.NodeServer != null)
                mqServer.NodeServer.SubscribeStartStop(server);
            
            return server;
        }
    }
}