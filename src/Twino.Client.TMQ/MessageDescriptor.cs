using System;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
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
        public abstract void Set(object value);
    }

    /// <summary>
    /// Followed acknowledge message descriptor 
    /// </summary>
    internal class AcknowledgeMessageDescriptor : MessageDescriptor
    {
        public TaskCompletionSource<bool> Source { get; }

        public AcknowledgeMessageDescriptor(TmqMessage message, DateTime expiration) : base(message, expiration)
        {
            Source = new TaskCompletionSource<bool>();
        }

        /// <inheritdoc />
        public override void Set(object value)
        {
            Source.SetResult(value != null);
        }
    }

    /// <summary>
    /// Followed response message descriptor 
    /// </summary>
    internal class ResponseMessageDescriptor : MessageDescriptor
    {
        public TaskCompletionSource<TmqMessage> Source { get; }

        public ResponseMessageDescriptor(TmqMessage message, DateTime expiration) : base(message, expiration)
        {
            Source = new TaskCompletionSource<TmqMessage>();
        }

        /// <inheritdoc />
        public override void Set(object value)
        {
            if (value == null)
                Source.SetResult(default);
            else
                Source.SetResult(value as TmqMessage);
        }
    }
}