using Horse.Core;
using Horse.Core.Protocols;

namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// Extension methods for Horse Protocol
    /// </summary>
    public static class HmqExtensions
    {
        /// <summary>
        /// Uses Horse Protocol and accepts TCP connections.
        /// </summary>
        public static IHorseServer UseHorseProtocol(this IHorseServer server, IProtocolConnectionHandler<HorseServerSocket, HorseMessage> handler)
        {
            HorseProtocol protocol = new HorseProtocol(server, handler);
            server.UseProtocol(protocol);
            return server;
        }

        /// <summary>
        /// Uses Horse Protocol and accepts TCP connections.
        /// </summary>
        public static IHorseServer UseHorseProtocol(this IHorseServer server, HorseMessageHandler action)
        {
            HorseMethodHandler handler = new HorseMethodHandler(action);
            HorseProtocol protocol = new HorseProtocol(server, handler);
            server.UseProtocol(protocol);
            return server;
        }
    }
}