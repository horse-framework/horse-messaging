using System;

namespace Twino.Client.TMQ.Models
{
    /// <summary>
    /// Read subscription source
    /// </summary>
    public enum ReadSource
    {
        /// <summary>
        /// Message source is queue, it's getting consumed
        /// </summary>
        Queue,
        
        /// <summary>
        /// Message source is another client, sending message directly
        /// </summary>
        Direct
    }
    /// <summary>
    /// Queue subscription meta data for message reader
    /// </summary>
    internal class ReadSubscription
    {
        /// <summary>
        /// Describes where the message comes from
        /// </summary>
        public ReadSource Source { get; set; }
        
        /// <summary>
        /// Subscribed channel
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Subscribed content type
        /// </summary>
        public ushort ContentType { get; set; }

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