namespace Twino.MQ.Data
{
    /// <summary>
    /// Decision for when messages are removed from disk persistently
    /// </summary>
    public enum DeleteWhen
    {
        /// <summary>
        /// After send at least one consumer. If message is sent through TCP connection successfully.
        /// </summary>
        AfterSend,

        /// <summary>
        /// After acknowledge is received from consumer.
        /// </summary>
        AfterAcknowledgeReceived
    }
}