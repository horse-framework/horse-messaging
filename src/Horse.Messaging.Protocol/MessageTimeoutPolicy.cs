using System.ComponentModel;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Queue Message Timeout Policy
/// </summary>
public enum MessageTimeoutPolicy
{
    /// <summary>
    /// Messages never timeout
    /// </summary>
    [Description("no-timeout")]
    NoTimeout,
    
    /// <summary>
    /// Messages are removed permanently when they timed out
    /// </summary>
    [Description("delete")]
    Delete,
    
    /// <summary>
    /// Messages are pushed to another queue when they timed out
    /// </summary>
    [Description("push")]
    PushQueue,
    
    /// <summary>
    /// Messages are published to a router when they timed out
    /// </summary>
    [Description("publish")]
    PublishRouter
}