using System;
using Horse.Messaging.Protocol.Events;

namespace Horse.Messaging.Client.Events.Annotations;

/// <summary>
/// Horse event attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class HorseEventAttribute : Attribute
{
    /// <summary>
    /// Event type
    /// </summary>
    public HorseEventType EventType { get; }

    /// <summary>
    /// Event target.
    /// Queue name, channel name etc
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Creates new Horse Event Attribute
    /// </summary>
    public HorseEventAttribute(HorseEventType eventType)
    {
        EventType = eventType;
    }

    /// <summary>
    /// Creates new Horse Event Attribute
    /// </summary>
    public HorseEventAttribute(HorseEventType eventType, string target)
    {
        EventType = eventType;
        Target = target;
    }
}