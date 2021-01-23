using System.Threading.Tasks;
using Horse.Mq.Clients;

namespace Horse.Mq
{
    /// <summary>
    /// Client connect and disconnect event implementations for MqServer
    /// </summary>
    public interface IClientHandler
    {
        /// <summary>
        /// Called when a client is connected and HMQ protocol handshake is completed
        /// </summary>
        Task Connected(HorseMq server, MqClient client);

        /// <summary>
        /// Called when a client is disconnected and removed from all queues
        /// </summary>
        Task Disconnected(HorseMq server, MqClient client);
    }
}