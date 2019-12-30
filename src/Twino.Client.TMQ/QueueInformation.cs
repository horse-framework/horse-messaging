namespace Twino.Client.TMQ
{
    /// <summary>
    /// Queue information
    /// </summary>
    public class QueueInformation
    {
        /// <summary>
        /// Queue channel name
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Queue id
        /// </summary>
        public ushort Id { get; set; }

        /// <summary>
        /// Pending high priority messages in the queue
        /// </summary>
        public int HighPriorityMessages { get; set; }

        /// <summary>
        /// Pending regular messages in the queue
        /// </summary>
        public int RegularMessages { get; set; }

        /// <summary>
        /// Queue current status
        /// </summary>
        public string Status { get; set; }
        
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