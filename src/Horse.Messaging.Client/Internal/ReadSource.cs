namespace Horse.Messaging.Client.Internal;

/// <summary>
/// Read subscription source
/// </summary>
internal enum ConsumeSource
{
    /// <summary>
    /// Message source is queue, it's getting consumed
    /// </summary>
    Queue,

    /// <summary>
    /// Message source is another client, sending message directly
    /// </summary>
    Direct,

    /// <summary>
    /// Message is a request and waits for response
    /// </summary>
    Request,

    /// <summary>
    /// Message source is channel
    /// </summary>
    Channel,
        
    /// <summary>
    /// Message source si event
    /// </summary>
    Event
}