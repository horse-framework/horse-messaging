using System;

namespace Horse.Messaging.Client.Queues.Annotations;

/// <summary>
/// Used for unique message id checks
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class UniqueIdCheckAttribute : Attribute
{
    /// <summary>
    /// If true, checks unique id
    /// </summary>
    public bool Value { get; set; }

    /// <summary>
    /// Used for unique message id checks
    /// </summary>
    public UniqueIdCheckAttribute() : this(true)
    {
    }

    /// <summary>
    /// Used for unique message id checks
    /// </summary>
    public UniqueIdCheckAttribute(bool value)
    {
        Value = value;
    }
}