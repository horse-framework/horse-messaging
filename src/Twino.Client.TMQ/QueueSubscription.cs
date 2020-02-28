using System;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Queue subscription meta data for message reader
    /// </summary>
    internal class QueueSubscription
    {
        /// <summary>
        /// Subscribed channel
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Subscribed content type
        /// </summary>
        public ushort QueueId { get; set; }

        /// <summary>
        /// Message type in the queue
        /// </summary>
        public Type MessageType { get; set; }

        /// <summary>
        /// The action that will triggered when the message received
        /// </summary>
        public Delegate Action { get; set; }

        /// <summary>
        /// True, if action has message parameter as second
        /// </summary>
        public bool TmqMessageParameter { get; set; }
    }
}