using System;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Queue name handler context
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
}