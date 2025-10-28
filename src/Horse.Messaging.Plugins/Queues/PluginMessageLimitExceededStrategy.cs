using System.ComponentModel;

namespace Horse.Messaging.Plugins.Queues;

/// <summary>
/// The strategy when message queue limit is exceed and a new message received.
/// </summary>
public enum PluginMessageLimitExceededStrategy
{
    /// <summary>
    /// Rejects new message and sends negative acknowledge to producer
    /// </summary>
    [Description("reject-new")]
    RejectNewMessage,

    /// <summary>
    /// Deletes oldest message from the queue and adds new message
    /// </summary>
    [Description("delete-oldest")]
    DeleteOldestMessage
}