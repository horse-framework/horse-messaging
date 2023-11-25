namespace Horse.Messaging.Server;

/// <summary>
/// Acknowledge message decision
/// </summary>
public enum AcknowledgeDecision
{
    /// <summary>
    /// Do nothing
    /// </summary>
    Nothing,

    /// <summary>
    /// Sends acknowledge message to it's owner
    /// </summary>
    SendToOwner
}