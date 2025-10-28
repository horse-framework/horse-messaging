namespace Horse.Messaging.Protocol.Models;

/// <summary>
/// Queue information
/// </summary>
public class QueueInformation
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
    /// Pending high priority messages in the queue
    /// </summary>
    public int PriorityMessages { get; set; }

    /// <summary>
    /// Actively processing message count by consumers.
    /// These messages are not in queue, just pending for acknowledge
    /// </summary>
    public int ProcessingMessages { get; set; }

    /// <summary>
    /// Count of the messages that are tracking by delivery tracker.
    /// These messages are being tracked because acknowledge is pending from their consumers.
    /// </summary>
    public int DeliveryTrackingMessages { get; set; }

    /// <summary>
    /// Pending regular messages in the queue
    /// </summary>
    public int Messages { get; set; }

    /// <summary>
    /// Queue current status
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Queue acknowledge type
    /// </summary>
    public string Acknowledge { get; set; }

    /// <summary>
    /// When acknowledge is required, maximum duration for waiting acknowledge message
    /// </summary>
    public int AcknowledgeTimeout { get; set; }

    /// <summary>
    /// When message queuing is active, maximum time for a message wait
    /// </summary>
    public MessageTimeoutStrategy MessageTimeout { get; set; }

    /// <summary>
    /// Total messages received from producers
    /// </summary>
    public long ReceivedMessages { get; set; }

    /// <summary>
    /// Total messages sent to consumers
    /// </summary>
    public long SentMessages { get; set; }

    /// <summary>
    /// Total unacknowledged messages
    /// </summary>
    public long NegativeAcks { get; set; }

    /// <summary>
    /// Total acknowledged messages
    /// </summary>
    public long Acks { get; set; }

    /// <summary>
    /// Total timed out messages
    /// </summary>
    public long TimeoutMessages { get; set; }

    /// <summary>
    /// Total saved messages
    /// </summary>
    public long SavedMessages { get; set; }

    /// <summary>
    /// Total removed messages
    /// </summary>
    public long RemovedMessages { get; set; }

    /// <summary>
    /// Total error count
    /// </summary>
    public long Errors { get; set; }

    /// <summary>
    /// Last message receive date in UNIX milliseconds
    /// </summary>
    public long LastMessageReceived { get; set; }

    /// <summary>
    /// Last message send date in UNIX milliseconds
    /// </summary>
    public long LastMessageSent { get; set; }

    /// <summary>
    /// Maximum message limit of the queue
    /// Zero is unlimited
    /// </summary>
    public int MessageLimit { get; set; }

    /// <summary>
    /// Message limit exceeded strategy
    /// </summary>
    public string LimitExceededStrategy { get; set; }

    /// <summary>
    /// Maximum message size limit
    /// Zero is unlimited
    /// </summary>
    public ulong MessageSizeLimit { get; set; }
        
    /// <summary>
    /// Delay in milliseconds between messages
    /// </summary>
    public int DelayBetweenMessages { get; set; }
}