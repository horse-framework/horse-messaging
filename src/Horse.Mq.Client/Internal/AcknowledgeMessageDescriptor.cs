using System;
using System.Threading.Tasks;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Internal
{
    /// <summary>
    /// Followed acknowledge message descriptor 
    /// </summary>
    internal class AcknowledgeMessageDescriptor : MessageDescriptor
    {
        public TaskCompletionSource<HorseResult> Source { get; }

        public AcknowledgeMessageDescriptor(HorseMessage message, DateTime expiration) : base(message, expiration)
        {
            Source = new TaskCompletionSource<HorseResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <inheritdoc />
        public override void Set(bool successful, object value)
        {
            if (SourceCompleted)
                return;

            SourceCompleted = true;
            Source.SetResult(successful ? HorseResult.Ok() : (HorseResult) value);
        }
    }
}