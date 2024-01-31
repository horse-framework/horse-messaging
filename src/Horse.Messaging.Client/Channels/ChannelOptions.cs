namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Channel options
/// </summary>
public class ChannelOptions
{
    /// <summary>
    /// If true, the channel is destroyed automatically when channel is inactive (no subscribers, no new published messages).
    /// Set null for server defaults. 
    /// </summary>
    public bool? AutoDestroy { get; set; }

    /// <summary>
    /// Maximum message size limit
    /// Zero is unlimited
    /// </summary>
    public ulong? MessageSizeLimit { get; set; }

    /// <summary>
    /// Maximum client limit of the queue
    /// Zero is unlimited
    /// </summary>
    public int? ClientLimit { get; set; }

    /// <summary>
    /// If true, the last published message is sent to clients right after they subscribed to the channel.
    /// </summary>
    public bool? SendLastMessageAsInitial { get; set; }

    /// <summary>
    /// If auto destroy is enabled, the idle time before destroy.
    /// Idle time means there is active produce or consume operations last n seconds.
    /// </summary>
    public int? AutoDestroyIdleSeconds { get; set; }
}