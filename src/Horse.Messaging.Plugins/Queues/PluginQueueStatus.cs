using System.ComponentModel;

namespace Horse.Messaging.Plugins.Queues;

/// <summary>
/// Queue status
/// </summary>
public enum PluginQueueStatus
{
    /// <summary>
    /// Queue is not initialized
    /// </summary>
    [Description("not-initialized")]
    NotInitialized,

    /// <summary>
    /// Queue initialized and running
    /// </summary>
    [Description("running")]
    Running,

    /// <summary>
    /// Queue allows to push but messages are not consumed
    /// </summary>
    [Description("only-push")]
    OnlyPush,

    /// <summary>
    /// Queue allows to consume but new messages are not allowed
    /// </summary>
    [Description("consume")]
    OnlyConsume,

    /// <summary>
    /// All push and consume operations are paused
    /// </summary>
    [Description("paused")]
    Paused,

    /// <summary>
    /// Queue messages are being synced
    /// </summary>
    [Description("syncing")]
    Syncing
}