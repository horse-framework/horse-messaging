using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Bus
{
    /// <summary>
    /// Implementation for sending messages to Twino MQ
    /// </summary>
    public interface ITwinoBus : ITwinoConnection
    {
        /// <summary>
        /// Implementation for direct messages and requests
        /// </summary>
        public ITwinoDirectBus Direct { get; }

        /// <summary>
        /// Implementation for queue messages and requests
        /// </summary>
        public ITwinoQueueBus Queue { get; }

        /// <summary>
        /// Implementation for route messages and requests
        /// </summary>
        public ITwinoRouteBus Route { get; }

        /// <summary>
        /// Sends a raw message
        /// </summary>
        Task<TwinoResult> SendAsync(TmqMessage message);

        /// <summary>
        /// Sends a raw message and waits for it's response
        /// </summary>
        /// <param name="message">Raw message</param>
        /// <returns>Response message</returns>
        Task<TmqMessage> RequestAsync(TmqMessage message);
    }
}