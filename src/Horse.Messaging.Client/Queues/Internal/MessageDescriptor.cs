using System;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues.Internal;

/// <summary>
/// Following messsage descriptor
/// </summary>
internal abstract class MessageDescriptor
{
    /// <summary>
    /// Message
    /// </summary>
    public HorseMessage Message { get; }

    /// <summary>
    /// Message follow expiration date
    /// </summary>
    public DateTime Expiration { get; }

    /// <summary>
    /// If true, message process is completed successfully (ack or response received)
    /// </summary>
    public bool Completed { get; set; }

    protected bool SourceCompleted { get; set; }

    protected MessageDescriptor(HorseMessage message, DateTime expiration)
    {
        Message = message;
        Expiration = expiration;
    }

    /// <summary>
    /// Sets message result
    /// </summary>
    public abstract void Set(bool successful, object value);
}