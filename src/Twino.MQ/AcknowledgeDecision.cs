namespace Twino.MQ
{
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
        /// Sends acknowldege message to it's owner
        /// </summary>
        SendToOwner
    }
}