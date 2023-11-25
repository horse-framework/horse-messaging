namespace Horse.Messaging.Server.Queues.Store;

/// <summary>
/// Message timeout tracker implementation.
/// That object removes timed out messages from message stores.
/// </summary>
public interface IMessageTimeoutTracker
{
    /// <summary>
    /// The message store
    /// </summary>
    IQueueMessageStore Store { get; }

    /// <summary>
    /// Starts to track messages
    /// </summary>
    void Start();

    /// <summary>
    /// Stops to track messages and releases all resources
    /// </summary>
    void Stop();
}