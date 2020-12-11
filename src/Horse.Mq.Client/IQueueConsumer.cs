using System.Threading.Tasks;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client
{
    /// <summary>
    /// Queue Consumer implementation.
    /// </summary>
    /// <typeparam name="TModel">Model type</typeparam>
    public interface IQueueConsumer<in TModel>
    {
        /// <summary>
        /// Consumes a message
        /// </summary>
        /// <param name="message">Raw HMQ message</param>
        /// <param name="model">Deserialized model</param>
        /// <param name="client">Connection client object</param>
        Task Consume(HorseMessage message, TModel model, HorseClient client);
    }
}