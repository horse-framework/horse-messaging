namespace Horse.Messaging.Protocol.Models;

/// <summary>
/// Channel information
/// </summary>
public class ChannelInformation
{
    /// <summary>
    /// Channel name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Channel topic
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Channel Status
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Total published message count by publishers
    /// </summary>
    public long Published
    {
        get => PublishedValue;
        set => PublishedValue = value;
    }

    /// <summary>
    /// Total receive count by subscribers.
    /// (Published Message Count x Subscriber Count) 
    /// An example, if there are 5 consumers in channel,
    /// When publisher publishes 10 messages, received value will be 50.
    /// </summary>
    public long Received
    {
        get => ReceivedValue;
        set => ReceivedValue = value;
    }

    /// <summary>
    /// Active subscriber count of the channel
    /// </summary>
    public int SubscriberCount { get; set; }

    internal long PublishedValue;
    internal long ReceivedValue;
}