using System.ComponentModel;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Describes how messages are routed
/// </summary>
public enum RouteMethod
{
    /// <summary>
    /// Routes each message to all bindings
    /// </summary>
    [Description("distribute")]
    Distribute = 0,

    /// <summary>
    /// Routes each message to only one binding
    /// </summary>
    [Description("round-robin")]
    RoundRobin = 1,

    /// <summary>
    /// Routes message to only first binding.
    /// Useful when you need only one queue can received messages at same time guarantee.
    /// Messages are sent to only one active queue when it exists.
    /// When it's removed messages are sent to other queue while it's active. 
    /// </summary>
    [Description("only-first")]
    OnlyFirst = 2
}