using System;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Queue name handler context.
/// Provides the original resolved queue name (from attributes or defaults)
/// along with the model type, so the handler can transform the name as needed.
/// </summary>
public class QueueNameHandlerContext
{
    /// <summary>
    /// Client
    /// </summary>
    public HorseClient Client { get; set; }

    /// <summary>
    /// Model type
    /// </summary>
    public Type Type { get; set; }

    /// <summary>
    /// Original queue name resolved from [QueueName] attribute or model type name.
    /// The handler can use this as a base to build the final queue name.
    /// </summary>
    public string QueueName { get; set; }
}