namespace Horse.Messaging.Server.Queues.Delivery;

/// <summary>
/// Sending decision message to producer
/// </summary>
public enum DecisionTransmission
{
    /// <summary>
    /// Do not send any message to producer
    /// </summary>
    None,

    /// <summary>
    /// Send commit message to the producer
    /// </summary>
    Commit,

    /// <summary>
    /// Send failed message to the producer
    /// </summary>
    Failed
}