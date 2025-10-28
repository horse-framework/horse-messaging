using System;

namespace Horse.Messaging.Client.Queues.Annotations;

/// <summary>
/// Moves messages into a error queue with exception description in additional content
/// </summary>
public class MoveOnErrorAttribute : Attribute
{
    /// <summary>
    /// The error queue name
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    /// Creates new move to error queue attribute
    /// </summary>
    public MoveOnErrorAttribute(string errorQueueName)
    {
        QueueName = errorQueueName;
    }
}