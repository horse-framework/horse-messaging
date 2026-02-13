using System;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Clients;

/// <summary>
/// Definition object of a client subscribed to a queue
/// </summary>
public class QueueClient
{
    internal int Index { get; set; }

    /// <summary>
    /// The time when client has subscribed to the queue
    /// </summary>
    public DateTime JoinDate { get; }

    /// <summary>
    /// Queue object
    /// </summary>
    public HorseQueue Queue { get; set; }

    /// <summary>
    /// Client object
    /// </summary>
    public MessagingClient Client { get; set; }

    /// <summary>
    /// Used when acknowledge is enabled for the queue.
    /// When consumer receives a message, this value keeps the message until consumer sends ack/nack or ack time out. 
    /// </summary>
    public QueueMessage CurrentlyProcessing { get; internal set; }

    /// <summary>
    /// Calculated deadline for acknowledge timeout of the message
    /// </summary>
    public DateTime ProcessDeadline { get; set; }

    /// <summary>
    /// Total consume count.
    /// If consumer consume same message twice, it's count two. 
    /// </summary>
    public long ConsumeCount { get; set; }

    /// <summary>
    /// Total consume acknowledge count
    /// </summary>
    public long AcknowledgeCount { get; set; }

    /// <summary>
    /// Total count of how many times consumer received a message but could not send ack in time
    /// </summary>
    public long AckTimeoutCount { get; set; }

    /// <summary>
    /// Creates new queue client pair descriptor
    /// </summary>
    public QueueClient(HorseQueue queue, MessagingClient client)
    {
        Queue = queue;
        Client = client;
        JoinDate = DateTime.UtcNow;
    }
}