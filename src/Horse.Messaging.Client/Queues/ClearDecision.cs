namespace Horse.Messaging.Client.Queues;

/// <summary>
/// After all messages received with pull operation
/// Clearing left messages in queue option
/// </summary>
public enum ClearDecision
{
    /// <summary>
    /// Do nothing. Do not delete any message.
    /// </summary>
    None,

    /// <summary>
    /// Clear all messages in queue
    /// </summary>
    AllMessages,

    /// <summary>
    /// Clear high priority messages in queue
    /// </summary>
    PriorityMessages,

    /// <summary>
    /// Clear default priority messages in queue
    /// </summary>
    Messages
}