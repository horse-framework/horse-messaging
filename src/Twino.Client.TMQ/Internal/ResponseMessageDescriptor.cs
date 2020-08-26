using System;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    /// <summary>
    /// Followed response message descriptor 
    /// </summary>
    internal class ResponseMessageDescriptor : MessageDescriptor
    {
        public TaskCompletionSource<TwinoMessage> Source { get; }

        public ResponseMessageDescriptor(TwinoMessage message, DateTime expiration) : base(message, expiration)
        {
            Source = new TaskCompletionSource<TwinoMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <inheritdoc />
        public override void Set(bool successful, object value)
        {
            if (SourceCompleted)
                return;

            SourceCompleted = true;
            
            if (!successful || value == null)
                Source.SetResult(default);
            else
                Source.SetResult(value as TwinoMessage);
        }
    }
}