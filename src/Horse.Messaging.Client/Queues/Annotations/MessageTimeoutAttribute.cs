using System;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues.Annotations;

/// <summary>
/// Used to specify message timeout for the queue
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class MessageTimeoutAttribute : Attribute
{
    /// <summary>
    /// Message timeout duration in seconds
    /// </summary>
    public int Duration { get; }

    /// <summary>
    /// Message timeout policy
    /// </summary>
    public MessageTimeoutPolicy Policy { get; }

    /// <summary>
    /// If policy is Push Queue or Publish Router, Queue or Router name
    /// </summary>
    public string TargetName { get; }

    /// <summary>
    /// Creates new message timeout attribute
    /// </summary>
    public MessageTimeoutAttribute(MessageTimeoutPolicy policy, int seconds)
    {
        Policy = policy;
        Duration = seconds;
    }

    /// <summary>
    /// Creates new message timeout attribute
    /// </summary>
    public MessageTimeoutAttribute(MessageTimeoutPolicy policy)
    {
        Policy = policy;
    }

    /// <summary>
    /// Creates new message timeout attribute
    /// </summary>
    public MessageTimeoutAttribute(MessageTimeoutPolicy policy, int seconds, string targetName)
    {
        Policy = policy;
        Duration = seconds;
        TargetName = targetName;
    }
}