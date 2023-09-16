using System;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues.Internal;

internal class CallbackMessageDescriptor : MessageDescriptor
{
    private readonly Action<HorseMessage, bool> _callback;

    public CallbackMessageDescriptor(HorseMessage message, DateTime expiration, Action<HorseMessage, bool> callback) : base(message, expiration)
    {
        _callback = callback;
    }

    /// <inheritdoc />
    public override void Set(bool successful, object value)
    {
        if (SourceCompleted)
            return;

        SourceCompleted = true;
        _callback(Message, successful);
    }
}