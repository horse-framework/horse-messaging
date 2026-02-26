namespace Horse.Messaging.Server.Stores;

/// <summary>
/// Horse Store Options
/// </summary>
public class StoreOptions
{
    /// <summary>
    /// Time range for each partition file.
    /// An example if the range is 900 seconds,
    /// the file may be deleted permanently if first message is older than retention seconds + 900 from now.
    /// </summary>
    public long FileTimeRangeSeconds { get; set; }
    
    /// <summary>
    /// Partition count of the store
    /// </summary>
    public int PartitionCount { get; set; }
    
    /// <summary>
    /// Store message retention in seconds
    /// </summary>
    public int RetentionSeconds { get; set; }
    
    /// <summary>
    /// The number of additional messages that will be delivered to the client,
    /// even though the client has not yet sent the offset commit.
    /// </summary>
    public int ClientOverloadMessageCount { get; set; }

    /// <summary>
    /// When a client receives a message, server waits commit message for the specified offset.
    /// If client does not send any commit message in specified time, it will be disconnected by the server.
    /// </summary>
    public int ConsumeExpirationSeconds { get; set; }
}