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
        /// Consumes a direct message
        /// </summary>
        /// <param name="message">Raw TMQ message</param>
        /// <param name="model">Deserialized model</param>
        Task Consume(TmqMessage message, TModel model);
    }
}