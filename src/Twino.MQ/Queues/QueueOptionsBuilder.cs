using System;
using System.Text;
using Twino.MQ.Options;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues
{
    public class QueueOptionsBuilder
    {
        /// <summary>
        /// Queue content type
        /// </summary>
        public ushort ContentType { get; set; }

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
        public QueueStatus? Status { get; set; }

        /// <summary>
        /// Serializes options and creates key value pair
        /// </summary>
        public string Serialize()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(Line(TmqHeaders.CONTENT_TYPE, ContentType.ToString()));

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

            return builder.ToString();
        }

        /// <summary>
        /// Loads options from serialized key-value lines
        /// </summary>
        public void Load(string serialized)
        {
            string[] lines = serialized.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] kv = line.Split(':');
                string key = kv[0].Trim();
                string value = kv[1].Trim();

                if (key.Equals(TmqHeaders.CONTENT_TYPE, StringComparison.InvariantCultureIgnoreCase))
                    ContentType = Convert.ToUInt16(value);

                if (key.Equals(TmqHeaders.ONLY_FIRST_ACQUIRER, StringComparison.InvariantCultureIgnoreCase))
                    SendOnlyFirstAcquirer = value == "1" || value == "true";

                if (key.Equals(TmqHeaders.REQUEST_ACKNOWLEDGE, StringComparison.InvariantCultureIgnoreCase))
                    RequestAcknowledge = value == "1" || value == "true";

                if (key.Equals(TmqHeaders.ACKNOWLEDGE_TIMEOUT, StringComparison.InvariantCultureIgnoreCase))
                    AcknowledgeTimeout = TimeSpan.FromMilliseconds(Convert.ToInt32(value));

                if (key.Equals(TmqHeaders.MESSAGE_TIMEOUT, StringComparison.InvariantCultureIgnoreCase))
                    MessageTimeout = TimeSpan.FromMilliseconds(Convert.ToInt32(value));

                if (key.Equals(TmqHeaders.USE_MESSAGE_ID, StringComparison.InvariantCultureIgnoreCase))
                    UseMessageId = value == "1" || value == "true";

                if (key.Equals(TmqHeaders.WAIT_FOR_ACKNOWLEDGE, StringComparison.InvariantCultureIgnoreCase))
                    WaitForAcknowledge = value == "1" || value == "true";

                if (key.Equals(TmqHeaders.HIDE_CLIENT_NAMES, StringComparison.InvariantCultureIgnoreCase))
                    HideClientNames = value == "1" || value == "true";

                if (key.Equals(TmqHeaders.QUEUE_STATUS, StringComparison.InvariantCultureIgnoreCase))
                {
                    switch (value)
                    {
                        case "route":
                            Status = QueueStatus.Route;
                            break;

                        case "push":
                            Status = QueueStatus.Push;
                            break;

                        case "pull":
                            Status = QueueStatus.Pull;
                            break;

                        case "pause":
                        case "paused":
                            Status = QueueStatus.Paused;
                            break;

                        case "stop":
                        case "stoped":
                        case "stopped":
                            Status = QueueStatus.Stopped;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Applies non-null values to channel queue options
        /// </summary>
        public void ApplyTo(ChannelQueueOptions target)
        {
            if (SendOnlyFirstAcquirer.HasValue)
                target.SendOnlyFirstAcquirer = SendOnlyFirstAcquirer.Value;

            if (RequestAcknowledge.HasValue)
                target.RequestAcknowledge = RequestAcknowledge.Value;

            if (AcknowledgeTimeout.HasValue)
                target.AcknowledgeTimeout = AcknowledgeTimeout.Value;

            if (MessageTimeout.HasValue)
                target.MessageTimeout = MessageTimeout.Value;

            if (UseMessageId.HasValue)
                target.UseMessageId = UseMessageId.Value;

            if (WaitForAcknowledge.HasValue)
                target.WaitForAcknowledge = WaitForAcknowledge.Value;

            if (HideClientNames.HasValue)
                target.HideClientNames = HideClientNames.Value;

            if (Status.HasValue)
                target.Status = Status.Value;
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