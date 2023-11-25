using System.ComponentModel;

namespace Horse.Messaging.Server.Queues;

/// <summary>
/// Auto queue destroy options
/// </summary>
public enum QueueDestroy
{
    /// <summary>
    /// Auto queue destroy is disabled
    /// </summary>
    [Description("disabled")]
    Disabled,

    /// <summary>
    /// Queue is destroyed when it's empty
    /// </summary>
    [Description("no-messages")]
    NoMessages,

    /// <summary>
    /// Queue is destroyed when there is no consumers
    /// </summary>
    [Description("no-consumers")]
    NoConsumers,

    /// <summary>
    /// Queue is destroyed when it's empty and there is no consumers
    /// </summary>
    [Description("empty")]
    Empty
}