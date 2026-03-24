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
    /// Topic name for the queue.
    /// If queue is not created, it will be created with specified topic
    /// </summary>
    public string QueueTopic { get; }


    /// <summary>
    /// Creates new move to error queue attribute
    /// </summary>
    public MoveOnErrorAttribute(string errorQueueName, string topicName = null)
    {
        QueueName = errorQueueName;
        QueueTopic = topicName;
    }
}