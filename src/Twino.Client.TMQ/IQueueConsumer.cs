using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Queue Consumer implementation.
    /// </summary>
    /// <typeparam name="TModel">Model type</typeparam>
    public interface IQueueConsumer<in TModel>
    {
        /// <summary>
        /// Consumes a message from a queue
        /// </summary>
        /// <param name="message">Raw TMQ message</param>
        /// <param name="model">Deserialized model</param>
        Task Consume(TmqMessage message, TModel model);
    }
}