using System;
using System.Collections.Generic;
using EnumsNET;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Type descriptor for queue messages
/// </summary>
public class QueueTypeDescriptor : ITypeDescriptor
{
    /// <summary>
    /// Message model type
    /// </summary>
    public Type Type { get; set; }

    /// <summary>
    /// If true, message is sent as high priority
    /// </summary>
    public bool HighPriority { get; set; }

    /// <summary>
    /// If queue is created with a message push and that value is not null, that option will be used
    /// </summary>
    public QueueAckDecision? Acknowledge { get; set; }

    /// <summary>
    /// If queue is created with a message push and that value is not null, queue will be created with that status
    /// </summary>
    public MessagingQueueType? QueueType { get; set; }

    /// <summary>
    /// If queue is created with a message push and that value is not null, queue topic.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Headers for delivery descriptor of type
    /// </summary>
    public List<KeyValuePair<string, string>> Headers { get; }

    /// <summary>
    /// Queue name for queue messages
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Delay between messages option (in milliseconds)
    /// </summary>
    public int? DelayBetweenMessages { get; set; }

    /// <summary>
    /// Put back decision
    /// </summary>
    public PutBack? PutBackDecision { get; set; }

    /// <summary>
    /// Put back delay in milliseconds
    /// </summary>
    public int? PutBackDelay { get; set; }

    /// <summary>
    /// True if type has QueueNameAttribute
    /// </summary>
    public bool HasQueueName { get; set; }

    /// <summary>
    /// Message timeout in seconds
    /// </summary>
    public MessageTimeoutStrategyInfo MessageTimeout { get; set; }

    /// <summary>
    /// Acknowledge timeout in seconds
    /// </summary>
    public int? AcknowledgeTimeout { get; set; }

    /// <summary>
    /// If true, server checks all message id values and reject new messages with same id.
    /// Enabling that feature has performance penalty about 0.03 ms for each message. 
    /// </summary>
    public bool? UniqueIdCheck { get; set; }

    /// <summary>
    /// Creates new type delivery descriptor
    /// </summary>
    public QueueTypeDescriptor()
    {
        Headers = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// Applies descriptor information to the message
    /// </summary>
    public HorseMessage CreateMessage(string overwrittenTarget = null)
    {
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, overwrittenTarget ?? QueueName, 0);
        if (HighPriority)
            message.HighPriority = HighPriority;

        if (Acknowledge.HasValue)
            message.AddHeader(HorseHeaders.ACKNOWLEDGE, Acknowledge.Value.AsString(EnumFormat.Description));

        if (HasQueueName)
            message.AddHeader(HorseHeaders.QUEUE_NAME, QueueName);

        if (QueueType.HasValue)
            message.AddHeader(HorseHeaders.QUEUE_TYPE, QueueType.Value.AsString(EnumFormat.Description));

        if (!string.IsNullOrEmpty(Topic))
            message.AddHeader(HorseHeaders.QUEUE_TOPIC, Topic);

        if (DelayBetweenMessages.HasValue)
            message.AddHeader(HorseHeaders.DELAY_BETWEEN_MESSAGES, DelayBetweenMessages.Value.ToString());

        if (PutBackDecision.HasValue)
            message.AddHeader(HorseHeaders.PUT_BACK, PutBackDecision.Value.AsString(EnumFormat.Description));

        if (PutBackDelay.HasValue)
            message.AddHeader(HorseHeaders.PUT_BACK_DELAY, PutBackDelay.Value.ToString());

        if (MessageTimeout != null)
        {
            string value = $"{MessageTimeout.MessageDuration};{MessageTimeout.Policy};{MessageTimeout.TargetName ?? string.Empty}";
            message.AddHeader(HorseHeaders.MESSAGE_TIMEOUT, value);
        }

        if (AcknowledgeTimeout.HasValue)
            message.AddHeader(HorseHeaders.ACK_TIMEOUT, AcknowledgeTimeout.Value.ToString());

        if (UniqueIdCheck.HasValue)
            message.AddHeader(HorseHeaders.MESSAGE_ID_UNIQUE_CHECK, UniqueIdCheck.Value ? "1" : "0");

        foreach (KeyValuePair<string, string> pair in Headers)
            message.AddHeader(pair.Key, pair.Value);

        return message;
    }
}