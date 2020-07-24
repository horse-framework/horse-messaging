using System;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Created as base interface. Use IQueueConsumer or IDirectConsumer instead
    /// </summary>
    [Obsolete("Created as base interface. Use IQueueConsumer or IDirectConsumer instead")]
    public interface ITwinoConsumer<in TModel>
    {
        /// <summary>
        /// Consumes a message
        /// </summary>
        /// <param name="message">Raw TMQ message</param>
        /// <param name="model">Deserialized model</param>
        /// <param name="client">Connection client object</param>
        Task Consume(TmqMessage message, TModel model, TmqClient client);
    }
}