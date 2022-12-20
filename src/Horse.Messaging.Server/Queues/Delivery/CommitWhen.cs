using System.ComponentModel;

namespace Horse.Messaging.Server.Queues.Delivery
{
    /// <summary>
    /// Enum for decision when commit will sent to producer
    /// </summary>
    public enum CommitWhen
    {
        /// <summary>
        /// Commit message is not sent
        /// </summary>
        [Description("none")]
        None,

        /// <summary>
        /// After producer sent message and server received it
        /// </summary>
        [Description("after-received")]
        AfterReceived,

        /// <summary>
        /// After message is sent to all consumers
        /// </summary>
        [Description("after-sent")]
        AfterSent,

        /// <summary>
        /// After each consumer sent acknowledge message
        /// </summary>
        [Description("after-ack")]
        AfterAcknowledge
    }
}