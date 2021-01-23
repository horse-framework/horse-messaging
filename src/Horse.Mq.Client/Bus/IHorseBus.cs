using System.Threading.Tasks;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Bus
{
    /// <summary>
    /// Implementation for sending messages to Horse MQ
    /// </summary>
    public interface IHorseBus : IHorseConnection
    {
        /// <summary>
        /// Implementation for direct messages and requests
        /// </summary>
        public IHorseDirectBus Direct { get; }

        /// <summary>
        /// Implementation for queue messages and requests
        /// </summary>
        public IHorseQueueBus Queue { get; }

        /// <summary>
        /// Implementation for route messages and requests
        /// </summary>
        public IHorseRouteBus Route { get; }

        /// <summary>
        /// Sends a raw message
        /// </summary>
        Task<HorseResult> SendAsync(HorseMessage message);

        /// <summary>
        /// Sends a raw message and waits for it's response
        /// </summary>
        /// <param name="message">Raw message</param>
        /// <returns>Response message</returns>
        Task<HorseMessage> RequestAsync(HorseMessage message);
    }
}