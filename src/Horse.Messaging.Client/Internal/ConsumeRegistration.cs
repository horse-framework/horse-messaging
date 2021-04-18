using System;
using Horse.Messaging.Client.Queues.Internal;

namespace Horse.Messaging.Client.Internal
{
    /// <summary>
    /// Queue subscription meta data for message reader
    /// </summary>
    internal class ConsumeRegistration
    {
        /// <summary>
        /// Describes where the message comes from
        /// </summary>
        public ConsumeSource Source { get; set; }

        /// <summary>
        /// Subscribed queue name
        /// </summary>
        public string Queue { get; set; }

        /// <summary>
        /// Subscribed content type
        /// </summary>
        public ushort ContentType { get; set; }

        /// <summary>
        /// Message type in the queue
        /// </summary>
        public Type MessageType { get; set; }

        /// <summary>
        /// Response message type in the queue
        /// </summary>
        public Type ResponseType { get; set; }

        /// <summary>
        /// True, if action has message parameter as second
        /// </summary>
        public bool HmqMessageParameter { get; set; }

        /// <summary>
        /// If subscription created via IQueueConsumer, the consuemr executer object for the consumer
        /// </summary>
        public ConsumerExecuter ConsumerExecuter { get; set; }
    }
}