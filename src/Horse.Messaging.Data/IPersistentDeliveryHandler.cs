using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Data
{
    /// <summary>
    /// Persistent deliery handler implementation includes default properties and methods
    /// </summary>
    public interface IPersistentDeliveryHandler : IMessageDeliveryHandler
    {
        /// <summary>
        /// Queue of the delivery handler
        /// </summary>
        HorseQueue Queue { get; }

        /// <summary>
        /// Database filename.
        /// If persistent delivery handler uses different save system value can be ignored.
        /// </summary>
        string DbFilename { get; }

        /// <summary>
        /// Option when to delete messages from disk
        /// </summary>
        DeleteWhen DeleteWhen { get; }

        /// <summary>
        /// Option when to send acknowledge to producer
        /// </summary>
        ProducerAckDecision ProducerAckDecision { get; }

        /// <summary>
        /// Initializes handler and loads the queue
        /// </summary>
        Task Initialize();
    }
}