using System;

namespace Horse.Messaging.Client.Annotations;

/// <summary>
/// Sets client limit for auto-created or auto-subscribed queues and channels.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ClientLimitAttribute : Attribute
{
    /// <summary>
    /// Maximum client limit. Zero means unlimited.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Creates a new client limit attribute.
    /// </summary>
    public ClientLimitAttribute(int value)
    {
        Value = value;
    }
}
