using System;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    internal abstract class MessageDescriptor
    {
        public TmqMessage Message { get; }
        public DateTime Expiration { get; }
        public bool Completed { get; set; }

        protected MessageDescriptor(TmqMessage message, DateTime expiration)
        {
            Message = message;
            Expiration = expiration;
        }

        public abstract void Set(object value);
    }

    internal class AcknowledgeMessageDescriptor : MessageDescriptor
    {
        public TaskCompletionSource<bool> Source { get; }

        public AcknowledgeMessageDescriptor(TmqMessage message, DateTime expiration) : base(message, expiration)
        {
            Source = new TaskCompletionSource<bool>();
        }

        public override void Set(object value)
        {
            Source.SetResult(value != null);
        }
    }

    internal class ResponseMessageDescriptor : MessageDescriptor
    {
        public TaskCompletionSource<TmqMessage> Source { get; }

        public ResponseMessageDescriptor(TmqMessage message, DateTime expiration) : base(message, expiration)
        {
            Source = new TaskCompletionSource<TmqMessage>();
        }

        public override void Set(object value)
        {
            if (value == null)
                Source.SetResult(default);
            else
                Source.SetResult(value as TmqMessage);
        }
    }
}