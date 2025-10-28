using System.ComponentModel;

namespace Horse.Messaging.Plugins.Channels;

/// <summary>
/// Channel status
/// </summary>
public enum PluginChannelStatus
{
    /// <summary>
    /// Channel is paused.
    /// New messages are not accepted.
    /// </summary>
    [Description("paused")]
    Paused,

    /// <summary>
    /// Channel is running, receiving and sending messages.
    /// </summary>
    [Description("running")]
    Running,

    /// <summary>
    /// Channel is destroyed
    /// </summary>
    [Description("destroyed")]
    Destroyed
}