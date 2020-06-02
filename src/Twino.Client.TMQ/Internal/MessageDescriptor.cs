using System;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    /// <summary>
    /// Following messsage descriptor
    /// </summary>
    internal abstract class MessageDescriptor
    {
        /// <summary>
        /// Message
        /// </summary>
        public TmqMessage Message { get; }

        /// <summary>
        /// Message follow expiration date
        /// </summary>
        public DateTime Expiration { get; }

        /// <summary>
        /// If true, message process is completed successfully (ack or response received)
        /// </summary>
        public bool Completed { get; set; }

        protected MessageDescriptor(TmqMessage message, DateTime expiration)
        {
            Message = message;
            Expiration = expiration;
        }

        /// <summary>
        /// Sets message result
        /// </summary>
        public abstract void Set(bool successful, object value);
    }
}