using System.ComponentModel;

namespace Horse.Messaging.Server.Queues;

/// <summary>
/// Queue status change decision enum
/// </summary>
public enum QueueStatusAction
{
    /// <summary>
    /// Denies the operation
    /// </summary>
    [Description("deny")]
    Deny,

    /// <summary>
    /// Allows the operation
    /// </summary>
    [Description("allow")]
    Allow,

    /// <summary>
    /// Denies the operation and calls trigger method in previos status
    /// </summary>
    [Description("deny-and-trigger")]
    DenyAndTrigger,

    /// <summary>
    /// Allows the operation and calls trigger method in next status
    /// </summary>
    [Description("allow-and-trigger")]
    AllowAndTrigger
}