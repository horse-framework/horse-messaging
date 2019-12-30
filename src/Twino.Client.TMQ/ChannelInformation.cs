namespace Twino.Client.TMQ
{
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
        /// Active queues in the channel
        /// </summary>
        public ushort[] Queues { get; set; }

        /// <summary>
        /// If true, channel can have multiple queues with multiple content type.
        /// If false, each channel can only have one queue
        /// </summary>
        public bool AllowMultipleQueues { get; set; } = true;

        /// <summary>
        /// Allowed queue id list for the channel
        /// </summary>
        public ushort[] AllowedQueues { get; set; }

        /// <summary>
        /// If true, messages will send to only first acquirers
        /// </summary>
        public bool SendOnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If true, messages will request acknowledge from receivers
        /// </summary>
        public bool RequestAcknowledge { get; set; }

        /// <summary>
        /// When acknowledge is required, maximum duration for waiting acknowledge message
        /// </summary>
        public int AcknowledgeTimeout { get; set; }

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        public int MessageTimeout { get; set; }

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        public bool UseMessageId { get; set; } = true;

        /// <summary>
        /// If true, queue does not send next message to receivers until acknowledge message received
        /// </summary>
        public bool WaitForAcknowledge { get; set; }

        /// <summary>
        /// If true, server doesn't send client name to receivers in queueus.
        /// </summary>
        public bool HideClientNames { get; set; }
    }
}