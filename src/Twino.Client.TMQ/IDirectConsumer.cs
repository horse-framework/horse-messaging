using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Directmessage Consumer implementation.
    /// </summary>
    /// <typeparam name="TModel">Model type</typeparam>
    public interface IDirectConsumer<in TModel>
    {
        /// <summary>
        /// Consumes a message
        /// </summary>
        /// <param name="message">Raw TMQ message</param>
        /// <param name="model">Deserialized model</param>
        /// <param name="client">Connection client object</param>
        Task Consume(TwinoMessage message, TModel model, TmqClient client);
    }
}