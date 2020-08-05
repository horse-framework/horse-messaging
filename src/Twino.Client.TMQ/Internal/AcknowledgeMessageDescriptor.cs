using System;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    /// <summary>
    /// Followed acknowledge message descriptor 
    /// </summary>
    internal class AcknowledgeMessageDescriptor : MessageDescriptor
    {
        public TaskCompletionSource<TwinoResult> Source { get; }

        public AcknowledgeMessageDescriptor(TmqMessage message, DateTime expiration) : base(message, expiration)
        {
            Source = new TaskCompletionSource<TwinoResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <inheritdoc />
        public override void Set(bool successful, object value)
        {
            if (SourceCompleted)
                return;

            SourceCompleted = true;
            Source.SetResult(successful ? TwinoResult.Ok() : (TwinoResult) value);
        }
    }
}