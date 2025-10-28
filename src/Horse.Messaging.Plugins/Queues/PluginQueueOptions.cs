using System;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Plugins.Queues;

/// <summary>
/// Queue options
/// </summary>
public class PluginQueueOptions
{
    /// <summary>
    /// Acknowledge decision. Default is just request.
    /// </summary>
    public QueueAckDecision Acknowledge { get; set; }

    /// <summary>
    /// Option for when the commit message is sent to producer
    /// </summary>
    public PluginCommitWhen CommitWhen { get; set; }

    /// <summary>
    /// When acknowledge is required, maximum duration for waiting acknowledge message
    /// </summary>
    public TimeSpan AcknowledgeTimeout { get; set; }

    /// <summary>
    /// When message queuing is active, maximum time for a message wait
    /// </summary>
    public MessageTimeoutStrategy MessageTimeout { get; set; } = new();

    /// <summary>
    /// Default type for the queue
    /// </summary>
    public PluginQueueType Type { get; set; }

    /// <summary>
    /// Maximum message limit of the queue
    /// Zero is unlimited
    /// </summary>
    public int MessageLimit { get; set; }

    /// <summary>
    /// Decision for when message limit is exceeded and a new message is received.
    /// Default value is RejectNewMessage.
    /// </summary>
    public PluginMessageLimitExceededStrategy LimitExceededStrategy { get; set; }

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
    public PluginPutBackDecision PutBack { get; set; }

    /// <summary>
    /// Waits in milliseconds before putting message back into the queue.
    /// Zero is no delay.
    /// </summary>
    public int PutBackDelay { get; set; }

    /// <summary>
    /// Queue auto destroy options. Default value is NoMessagesAndConsumers.
    /// </summary>
    public PluginQueueDestroy AutoDestroy { get; set; }

    /// <summary>
    /// If true, queue will be created automatically with default options
    /// when a client tries to subscribe or push a message to not existing queue.
    /// </summary>
    public bool AutoQueueCreation { get; set; }

    /// <summary>
    /// If true, server checks all message id values and reject new messages with same id.
    /// Enabling that feature has performance penalty about 0.03 ms for each message. 
    /// </summary>
    public bool MessageIdUniqueCheck { get; set; }
}