using System;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol.Events;

namespace Horse.Messaging.Client.Events;

/// <summary>
/// Registration definition for event subscriptions
/// </summary>
public class EventSubscriberRegistration
{
    /// <summary>
    /// Subscribed event type
    /// </summary>
    public HorseEventType Type { get; set; }

    /// <summary>
    /// Event target
    /// </summary>
    public string Target { get; set; }
        
    /// <summary>
    /// IHorseEventHandler type
    /// </summary>
    public Type HandlerType { get; set; }
        
    /// <summary>
    /// Handler executor
    /// </summary>
    public ExecutorBase Executer { get; set; }
}