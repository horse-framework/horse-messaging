namespace Horse.Messaging.Server.Cluster;

/// <summary>
/// Waiting acknowledge option while sending the queue message to replicated queues on other nodes
/// </summary>
public enum ReplicaAcknowledge
{
    /// <summary>
    /// Do not wait any acknowledge from other nodes.
    /// It's fastest operation without any guarantee
    /// </summary>
    None,

    /// <summary>
    /// Waits ack from only successor.
    /// It's faster from waiting for all nodes.
    /// But there is no guarantee when successor crashes.
    /// </summary>
    OnlySuccessor,

    /// <summary>
    /// Waits ack from all nodes.
    /// It guarantees all nodes received the message.
    /// </summary>
    AllNodes
}