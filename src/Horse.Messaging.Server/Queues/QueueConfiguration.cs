using System;
using EnumsNET;

namespace Horse.Messaging.Server.Queues;

/// <summary>
/// Persistent queue configuration data
/// </summary>
public class QueueConfiguration
{
    /// <summary>
    /// Queue name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Queue topic
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Queue Manager Name
    /// </summary>
    public string ManagerName { get; set; }

    /// <summary>
    /// Queue status
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Payload value is used by queue manager.
    /// Each queue manager can keep it's persistent configurations in that property
    /// </summary>
    public string ManagerPayload { get; set; }

    /// <summary>
    /// Acknowledge decision. Default is just request.
    /// </summary>
    public string Acknowledge { get; set; }

    /// <summary>
    /// Option for when the commit message is sent to producer
    /// </summary>
    public string CommitWhen { get; set; }

    /// <summary>
    /// When acknowledge is required, maximum duration for waiting acknowledge message (in milliseconds)
    /// </summary>
    public long AcknowledgeTimeout { get; set; }

    /// <summary>
    /// When message queuing is active, maximum time for a message wait (in milliseconds)
    /// </summary>
    public long MessageTimeout { get; set; }

    /// <summary>
    /// Default type for the queue
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Maximum message limit of the queue
    /// Zero is unlimited
    /// </summary>
    public int MessageLimit { get; set; }

    /// <summary>
    /// Decision for when message limit is exceeded and a new message is received.
    /// Default value is RejectNewMessage.
    /// </summary>
    public string LimitExceededStrategy { get; set; }

    /// <summary>
    /// Maximum message size limit
    /// Zero is unlimited
    /// </summary>
    public ulong MessageSizeLimit { get; set; }

    /// <summary>
    /// Maximum client limit of the queue
    /// Zero is unlimited
    /// </summary>
    public int ClientLimit { get; set; }

    /// <summary>
    /// Waits in milliseconds after sending each message to it's consumers.
    /// Zero is no delay.
    /// </summary>
    public int DelayBetweenMessages { get; set; }

    /// <summary>
    /// Queue put back decision option
    /// </summary>
    public string PutBack { get; set; }

    /// <summary>
    /// Waits in milliseconds before putting message back into the queue.
    /// Zero is no delay.
    /// </summary>
    public int PutBackDelay { get; set; }

    /// <summary>
    /// Queue auto destroy options. Default value is NoMessagesAndConsumers.
    /// </summary>
    public string AutoDestroy { get; set; }
    
    /// <summary>
    /// Creates new queue configuration from queue
    /// </summary>
    public static QueueConfiguration Create(HorseQueue queue)
    {
        return new QueueConfiguration
        {
            Name = queue.Name,
            Topic = queue.Topic,
            ManagerName = queue.ManagerName,
            Status = queue.Status.AsString(EnumFormat.Description),
            ManagerPayload = queue.ManagerPayload,
            Acknowledge = queue.Options.Acknowledge.AsString(EnumFormat.Description),
            Type = queue.Options.Type.AsString(EnumFormat.Description),
            AutoDestroy = queue.Options.AutoDestroy.AsString(EnumFormat.Description),
            CommitWhen = queue.Options.CommitWhen.AsString(EnumFormat.Description),
            PutBack = queue.Options.PutBack.AsString(EnumFormat.Description),
            LimitExceededStrategy = queue.Options.LimitExceededStrategy.AsString(EnumFormat.Description),
            AcknowledgeTimeout = Convert.ToInt64(queue.Options.AcknowledgeTimeout.TotalMilliseconds),
            ClientLimit = queue.Options.ClientLimit,
            MessageLimit = queue.Options.MessageLimit,
            DelayBetweenMessages = queue.Options.DelayBetweenMessages,
            PutBackDelay = queue.Options.PutBackDelay,
            MessageSizeLimit = queue.Options.MessageSizeLimit,
            MessageTimeout = Convert.ToInt64(queue.Options.AcknowledgeTimeout.TotalMilliseconds)
        };
    }
}