using System.Threading.Tasks;
using Twino.MQ.Queues;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Persistent deliery handler implementation includes default properties and methods
    /// </summary>
    public interface IPersistentDeliveryHandler : IMessageDeliveryHandler
    {
        /// <summary>
        /// Queue of the delivery handler
        /// </summary>
        TwinoQueue Queue { get; }
        
        /// <summary>
        /// Key for delivery handler attribute
        /// </summary>
        string Key { get; }

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