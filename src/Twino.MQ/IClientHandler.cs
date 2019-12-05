using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ
{
    /// <summary>
    /// Client connect and disconnect event implementations for MqServer
    /// </summary>
    public interface IClientHandler
    {
        /// <summary>
        /// Called when a client is connected and TMQ protocol handshake is completed
        /// </summary>
        Task Connected(MqServer server, MqClient client);
        
        /// <summary>
        /// Called when a client is disconnected and removed from all channels and all queues
        /// </summary>
        Task Disconnected(MqServer server, MqClient client);
    }
}