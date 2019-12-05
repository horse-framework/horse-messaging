using System;

namespace Twino.MQ.Options
{
    /// <summary>
    /// Queue options
    /// </summary>
    public class ChannelQueueOptions
    {
        /// <summary>
        /// If true, messages will be queued and wait for subscribers to deliver.
        /// If false, only online subsribers receive messages. If there is no subscriber message won't kept.
        /// </summary>
        public bool MessageQueuing { get; set; }

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
        public TimeSpan AcknowledgeTimeout { get; set; }

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        public TimeSpan MessagePendingTimeout { get; set; }

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        public bool UseMessageId { get; set; }
        
        /// <summary>
        /// If true, queue does not send next message to receivers until acknowledge message received
        /// </summary>
        public bool WaitAcknowledge { get; set; }
    }
}