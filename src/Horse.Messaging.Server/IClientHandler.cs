using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Client connect and disconnect event implementations for MqServer
    /// </summary>
    public interface IClientHandler
    {
        /// <summary>
        /// Called when a client is connected and Horse protocol handshake is completed
        /// </summary>
        Task Connected(HorseRider server, MessagingClient client);

        /// <summary>
        /// Called when a client is disconnected and removed from all queues
        /// </summary>
        Task Disconnected(HorseRider server, MessagingClient client);
    }
}