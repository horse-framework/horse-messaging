using Horse.Core;
using Horse.Core.Protocols;

namespace Horse.Protocols.Hmq
{
    /// <summary>
    /// Extension methods for HMQ Protocol
    /// </summary>
    public static class HmqExtensions
    {
        /// <summary>
        /// Uses HMQ Protocol and accepts TCP connections.
        /// </summary>
        public static IHorseServer UseHmq(this IHorseServer server, IProtocolConnectionHandler<HorseServerSocket, HorseMessage> handler)
        {
            HorseMqProtocol protocol = new HorseMqProtocol(server, handler);
            server.UseProtocol(protocol);
            return server;
        }

        /// <summary>
        /// Uses HMQ Protocol and accepts TCP connections.
        /// </summary>
        public static IHorseServer UseHmq(this IHorseServer server, HorseMessageHandler action)
        {
            HorseMethodHandler handler = new HorseMethodHandler(action);
            HorseMqProtocol protocol = new HorseMqProtocol(server, handler);
            server.UseProtocol(protocol);
            return server;
        }
    }
}