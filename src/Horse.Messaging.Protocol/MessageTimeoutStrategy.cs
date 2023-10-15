namespace Horse.Messaging.Protocol;

/// <summary>
/// Information class for message timeout strategy
/// </summary>
/// <param name="MessageDuration">Duration in seconds</param>
/// <param name="Policy">Delete policies; "no-timeout", "delete", "push", "publish"</param>
/// <param name="TargetName">If policy is push, the queue name. If policy is publish, router name. Otherwise null or empty.</param>
public record MessageTimeoutStrategyInfo(int MessageDuration, string Policy, string TargetName = "");

/// <summary>
/// Message timeout strategy for queues
/// </summary>
public class MessageTimeoutStrategy
{
    /// <summary>
    /// Message timeout duration in seconds
    /// </summary>
    public int MessageDuration { get; set; }

    /// <summary>
    /// Message timeout policy
    /// </summary>
    public MessageTimeoutPolicy Policy { get; set; } = MessageTimeoutPolicy.NoTimeout;

    /// <summary>
    /// Target name (queue or router name).
    /// Unused if there is no timeout or policy is delete.
    /// </summary>
    public string TargetName { get; set; }
}