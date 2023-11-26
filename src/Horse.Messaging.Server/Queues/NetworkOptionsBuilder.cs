using System;
using System.Text.Json.Serialization;
using EnumsNET;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Queues;

/// <summary>
/// Build options object with data over network
/// </summary>
public class NetworkOptionsBuilder
{
    #region Properties

    /// <summary>
    /// Acknowledge decisions : "none", "request", "wait" 
    /// </summary>
    [JsonPropertyName("Acknowledge")]
    public string Acknowledge { get; set; }

    /// <summary>
    /// When acknowledge is required, maximum duration for waiting acknowledge message
    /// </summary>
    [JsonPropertyName("AcknowledgeTimeout")]
    public int? AcknowledgeTimeout { get; set; }

    /// <summary>
    /// When message queuing is active, maximum time for a message wait
    /// </summary>
    [JsonPropertyName("MessageTimeout")]
    public MessageTimeoutStrategyInfo MessageTimeout { get; set; }

    /// <summary>
    /// Default type for the queue
    /// </summary>
    [JsonPropertyName("Type")]
    public QueueType? Type { get; set; }

    /// <summary>
    /// Maximum message limit of the queue
    /// Zero is unlimited
    /// </summary>
    [JsonPropertyName("MessageLimit")]
    public int? MessageLimit { get; set; }

    /// <summary>
    /// Maximum client limit of the queue
    /// Zero is unlimited
    /// </summary>
    [JsonPropertyName("ClientLimit")]
    public int? ClientLimit { get; set; }

    /// <summary>
    /// Maximum queue limit of the server
    /// Zero is unlimited
    /// </summary>
    [JsonPropertyName("QueueLimit")]
    public int? QueueLimit { get; set; }

    /// <summary>
    /// Queue auto destroy options
    /// </summary>
    [JsonPropertyName("AutoDestroy")]
    public string AutoDestroy { get; set; }

    /// <summary>
    /// Message limit exceeded strategy
    /// </summary>
    [JsonPropertyName("LimitExceededStrategy")]
    public string LimitExceededStrategy { get; set; }

    /// <summary>
    /// Delay between messages in milliseconds.
    /// Useful when wait for acknowledge is disabled but you need to prevent overheat on consumers if producer pushes too many messages in a short duration.
    /// Zero is no delay.
    /// </summary>
    [JsonPropertyName("DelayBetweenMessages")]
    public int? DelayBetweenMessages { get; set; }

    /// <summary>
    /// Waits in milliseconds before putting message back into the queue.
    /// Zero is no delay.
    /// </summary>
    [JsonPropertyName("PutBackDelay")]
    public int? PutBackDelay { get; set; }

    /// <summary>
    /// If true, server checks all message id values and reject new messages with same id.
    /// Enabling that feature has performance penalty about 0.03 ms for each message. 
    /// </summary>
    [JsonPropertyName("MessageIdUniqueCheck")]
    public bool? MessageIdUniqueCheck { get; set; }

    #endregion

    #region Apply

    /// <summary>
    /// Applies non-null values to queue options
    /// </summary>
    public void ApplyToQueue(QueueOptions target)
    {
        if (!string.IsNullOrEmpty(Acknowledge))
            target.Acknowledge = Enums.Parse<QueueAckDecision>(Acknowledge, true, EnumFormat.Description);

        if (!string.IsNullOrEmpty(AutoDestroy))
            target.AutoDestroy = Enums.Parse<QueueDestroy>(AutoDestroy, true, EnumFormat.Description);

        if (AcknowledgeTimeout.HasValue)
            target.AcknowledgeTimeout = TimeSpan.FromMilliseconds(AcknowledgeTimeout.Value);

        if (MessageTimeout != null)
            target.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = MessageTimeout.MessageDuration,
                Policy = Enums.Parse<MessageTimeoutPolicy>(MessageTimeout.Policy, true, EnumFormat.Description),
                TargetName = MessageTimeout.TargetName
            };

        if (Type.HasValue)
            target.Type = Type.Value;

        if (MessageLimit.HasValue)
            target.MessageLimit = MessageLimit.Value;

        if (!string.IsNullOrEmpty(LimitExceededStrategy))
            target.LimitExceededStrategy = Enums.Parse<MessageLimitExceededStrategy>(LimitExceededStrategy, true, EnumFormat.Description);

        if (ClientLimit.HasValue)
            target.ClientLimit = ClientLimit.Value;

        if (DelayBetweenMessages.HasValue)
            target.DelayBetweenMessages = DelayBetweenMessages.Value;

        if (PutBackDelay.HasValue)
            target.PutBackDelay = PutBackDelay.Value;

        if (MessageIdUniqueCheck.HasValue)
            target.MessageIdUniqueCheck = MessageIdUniqueCheck.Value;
    }

    #endregion
}