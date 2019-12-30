using System;
using System.Text;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Queue status
    /// </summary>
    public enum MessagingQueueStatus
    {
        /// <summary>
        /// Queue messaging is in running state.
        /// Messages are not queued, producers push the message and if there are available consumers, message is sent to them.
        /// Otherwise, message is deleted.
        /// If you need to keep messages and transmit only live messages, Route is good status to consume less resource.
        /// </summary>
        Route,

        /// <summary>
        /// Queue messaging is in running state.
        /// Producers push the message into the queue and consumer receive when message is pushed
        /// </summary>
        Push,

        /// <summary>
        /// Load balancing status. Queue messaging is in running state.
        /// Producers push the message into the queue and consumer receive when message is pushed.
        /// If there are no available consumers, message will be kept in queue like push status.
        /// </summary>
        RoundRobin,

        /// <summary>
        /// Queue messaging is in running state.
        /// Producers push message into queue, consumers receive the messages when they requested.
        /// Each message is sent only one-receiver at same time.
        /// Request operation removes the message from the queue.
        /// </summary>
        Pull,

        /// <summary>
        /// Queue messages are accepted from producers but they are not sending to consumers even they request new messages. 
        /// </summary>
        Paused,

        /// <summary>
        /// Queue messages are removed, producers can't push any message to the queue and consumers can't receive any message
        /// </summary>
        Stopped
    }

    public class QueueOptions
    {
        /// <summary>
        /// If true, messages will send to only first acquirers
        /// </summary>
        public bool? SendOnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If true, messages will request acknowledge from receivers
        /// </summary>
        public bool? RequestAcknowledge { get; set; }

        /// <summary>
        /// When acknowledge is required, maximum duration for waiting acknowledge message
        /// </summary>
        public TimeSpan? AcknowledgeTimeout { get; set; }

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        public TimeSpan? MessageTimeout { get; set; }

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        public bool? UseMessageId { get; set; } = true;

        /// <summary>
        /// If true, queue does not send next message to receivers until acknowledge message received
        /// </summary>
        public bool? WaitForAcknowledge { get; set; }

        /// <summary>
        /// If true, server doesn't send client name to receivers in queueus.
        /// </summary>
        public bool? HideClientNames { get; set; }

        /// <summary>
        /// Default status for the queue
        /// </summary>
        public MessagingQueueStatus? Status { get; set; }

        /// <summary>
        /// Registry key for message delivery handler
        /// </summary>
        public string MessageDeliveryHandler { get; set; }

        /// <summary>
        /// Serializes options and creates key value pair
        /// </summary>
        public string Serialize(ushort contentType)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(Line(TmqHeaders.CONTENT_TYPE, contentType.ToString()));

            if (SendOnlyFirstAcquirer.HasValue)
                builder.Append(Line(TmqHeaders.ONLY_FIRST_ACQUIRER, SendOnlyFirstAcquirer.Value));

            if (RequestAcknowledge.HasValue)
                builder.Append(Line(TmqHeaders.REQUEST_ACKNOWLEDGE, RequestAcknowledge.Value));

            if (AcknowledgeTimeout.HasValue)
                builder.Append(Line(TmqHeaders.ACKNOWLEDGE_TIMEOUT, AcknowledgeTimeout.Value));

            if (MessageTimeout.HasValue)
                builder.Append(Line(TmqHeaders.MESSAGE_TIMEOUT, MessageTimeout.Value));

            if (UseMessageId.HasValue)
                builder.Append(Line(TmqHeaders.USE_MESSAGE_ID, UseMessageId.Value));

            if (WaitForAcknowledge.HasValue)
                builder.Append(Line(TmqHeaders.WAIT_FOR_ACKNOWLEDGE, WaitForAcknowledge.Value));

            if (HideClientNames.HasValue)
                builder.Append(Line(TmqHeaders.HIDE_CLIENT_NAMES, HideClientNames.Value));

            if (Status.HasValue)
                builder.Append(Line(TmqHeaders.QUEUE_STATUS, Status.Value.ToString().ToLower()));

            if (!string.IsNullOrEmpty(MessageDeliveryHandler))
                builder.Append(Line(TmqHeaders.MESSAGE_DELIVERY_HANDLER, MessageDeliveryHandler));

            return builder.ToString();
        }

        private static string Line(string key, bool value)
        {
            return key + ": " + (value ? "1" : "0") + "\r\n";
        }

        private static string Line(string key, string value)
        {
            return key + ": " + value + "\r\n";
        }

        private static string Line(string key, TimeSpan value)
        {
            int ms = Convert.ToInt32(value.TotalMilliseconds.ToString());
            return key + ": " + ms + "\r\n";
        }
    }
}