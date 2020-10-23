using Twino.MQ.Queues;

namespace Twino.MQ.Data.Configuration
{
    /// <summary>
    /// Used while loading persistent queues from disk.
    /// Includes information about the queue
    /// </summary>
    public class LoadingQueueConfig
    {
        /// <summary>
        /// Database options of the queue
        /// </summary>
        public DatabaseOptions DatabaseOptions { get; set; }

        /// <summary>
        /// The queue that will use delivery handler
        /// </summary>
        public TwinoQueue Queue { get; internal set; }

        /// <summary>
        /// Delivery Handler Key of the queue
        /// </summary>
        public string DeliveryHandler { get; set; }

        /// <summary>
        /// Option when to delete messages from disk
        /// </summary>
        public DeleteWhen DeleteWhen { get; set; }

        /// <summary>
        /// Option when to send acknowledge to producer
        /// </summary>
        public ProducerAckDecision ProducerAck { get; set; }
    }
}