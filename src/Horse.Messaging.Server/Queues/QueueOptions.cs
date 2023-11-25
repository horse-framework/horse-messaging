using System;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues;

/// <summary>
/// Queue options
/// </summary>
public class QueueOptions
{
    /// <summary>
    /// Acknowledge decision. Default is just request.
    /// </summary>
    public QueueAckDecision Acknowledge { get; set; } = QueueAckDecision.WaitForAcknowledge;

    /// <summary>
    /// Option for when the commit message is sent to producer
    /// </summary>
    public CommitWhen CommitWhen { get; set; } = CommitWhen.AfterReceived;

    /// <summary>
    /// When acknowledge is required, maximum duration for waiting acknowledge message
    /// </summary>
    public TimeSpan AcknowledgeTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// When message queuing is active, maximum time for a message wait
    /// </summary>
    public MessageTimeoutStrategy MessageTimeout { get; set; } = new();

    /// <summary>
    /// Default type for the queue
    /// </summary>
    public QueueType Type { get; set; } = QueueType.RoundRobin;

    /// <summary>
    /// Maximum message limit of the queue
    /// Zero is unlimited
    /// </summary>
    public int MessageLimit { get; set; }

    /// <summary>
    /// Decision for when message limit is exceeded and a new message is received.
    /// Default value is RejectNewMessage.
    /// </summary>
    public MessageLimitExceededStrategy LimitExceededStrategy { get; set; } = MessageLimitExceededStrategy.RejectNewMessage;

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
    public PutBackDecision PutBack { get; set; } = PutBackDecision.Regular;

    /// <summary>
    /// Waits in milliseconds before putting message back into the queue.
    /// Zero is no delay.
    /// </summary>
    public int PutBackDelay { get; set; }

    /// <summary>
    /// Queue auto destroy options. Default value is NoMessagesAndConsumers.
    /// </summary>
    public QueueDestroy AutoDestroy { get; set; } = QueueDestroy.Disabled;

    /// <summary>
    /// If true, queue will be created automatically with default options
    /// when a client tries to subscribe or push a message to not existing queue.
    /// </summary>
    public bool AutoQueueCreation { get; set; } = true;

    /// <summary>
    /// If true, server checks all message id values and reject new messages with same id.
    /// Enabling that feature has performance penalty about 0.03 ms for each message. 
    /// </summary>
    public bool MessageIdUniqueCheck { get; set; }

    /// <summary>
    /// Creates clone of the object
    /// </summary>
    /// <returns></returns>
    internal object Clone()
    {
        return MemberwiseClone();
    }

    /// <summary>
    /// Clones queue options from another options
    /// </summary>
    public static QueueOptions CloneFrom(QueueOptions options)
    {
        return new QueueOptions
        {
            Type = options.Type,
            AcknowledgeTimeout = options.AcknowledgeTimeout,
            MessageTimeout = options.MessageTimeout,
            Acknowledge = options.Acknowledge,
            MessageLimit = options.MessageLimit,
            LimitExceededStrategy = options.LimitExceededStrategy,
            MessageSizeLimit = options.MessageSizeLimit,
            DelayBetweenMessages = options.DelayBetweenMessages,
            PutBackDelay = options.PutBackDelay,
            AutoDestroy = options.AutoDestroy,
            ClientLimit = options.ClientLimit,
            CommitWhen = options.CommitWhen,
            PutBack = options.PutBack,
            AutoQueueCreation = options.AutoQueueCreation,
            MessageIdUniqueCheck = options.MessageIdUniqueCheck
        };
    }
}