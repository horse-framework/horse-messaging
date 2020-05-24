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
            NetworkMessageHandler handler = new NetworkMessageHandler(mqServer);
            mqServer.Server = server;

            mqServer.InstanceManager.ConnectionHandler = new NodeConnectionHandler(mqServer.InstanceManager, handler);
            server.UseTmq(handler);

            if (mqServer.InstanceManager != null)
                mqServer.InstanceManager.SubscribeStartStop(server);

            return server;
        }
    }
}