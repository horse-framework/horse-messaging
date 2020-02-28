using System;
using System.Text;
using Twino.MQ.Options;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues
{
    /// <summary>
    /// Build options object with data over network
    /// </summary>
    public class NetworkOptionsBuilder
    {
        #region Properties

        /// <summary>
        /// Queue content type
        /// </summary>
        public ushort Id { get; set; }

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
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        public int? MessageLimit { get; set; }

        /// <summary>
        /// Maximum client limit of the channel
        /// Zero is unlimited
        /// </summary>
        public int? ClientLimit { get; set; }

        /// <summary>
        /// Maximum queue limit of the channel
        /// Zero is unlimited
        /// </summary>
        public int? QueueLimit { get; set; }

        /// <summary>
        /// If true, channel will be destroyed when there are no messages in queues and there are no consumers available
        /// </summary>
        public bool? DestroyWhenEmpty { get; set; }

        /// <summary>
        /// Registry key for message delivery handler
        /// </summary>
        public string MessageDeliveryHandler { get; set; }

        /// <summary>
        /// Registry key for channel event handler
        /// </summary>
        public string ChannelEventHandler { get; set; }

        /// <summary>
        /// Registry key for channel authenticator
        /// </summary>
        public string ChannelAuthenticator { get; set; }

        /// <summary>
        /// If true, channel supports multiple queues
        /// </summary>
        public bool? AllowMultipleQueues { get; set; }

        /// <summary>
        /// Allowed queues for channel
        /// </summary>
        public ushort[] AllowedQueues { get; set; }

        #endregion

        #region Serialization

        /// <summary>
        /// Serializes options and creates key value pair
        /// </summary>
        public string Serialize()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(Line(TmqHeaders.CONTENT_TYPE, Id.ToString()));

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

            if (AllowMultipleQueues.HasValue)
                builder.Append(Line(TmqHeaders.ALLOW_MULTIPLE_QUEUES, AllowMultipleQueues.Value));

            if (AllowedQueues != null)
            {
                string list = "";
                foreach (ushort aq in AllowedQueues)
                    list += aq + ",";

                if (list.EndsWith(","))
                    list = list.Substring(0, list.Length - 1);

                builder.Append(Line(TmqHeaders.ALLOWED_QUEUES, list));
            }

            if (DestroyWhenEmpty.HasValue)
                builder.Append(Line(TmqHeaders.DESTROY_WHEN_EMPTY, DestroyWhenEmpty.Value));

            if (MessageLimit.HasValue)
                builder.Append(Line(TmqHeaders.MESSAGE_LIMIT, MessageLimit.Value.ToString()));

            if (ClientLimit.HasValue)
                builder.Append(Line(TmqHeaders.CLIENT_LIMIT, ClientLimit.Value.ToString()));

            if (QueueLimit.HasValue)
                builder.Append(Line(TmqHeaders.QUEUE_LIMIT, QueueLimit.Value.ToString()));

            if (!string.IsNullOrEmpty(MessageDeliveryHandler))
                builder.Append(Line(TmqHeaders.MESSAGE_DELIVERY_HANDLER, MessageDeliveryHandler));

            if (!string.IsNullOrEmpty(ChannelEventHandler))
                builder.Append(Line(TmqHeaders.CHANNEL_EVENT_HANDLER, ChannelEventHandler));

            if (!string.IsNullOrEmpty(ChannelAuthenticator))
                builder.Append(Line(TmqHeaders.CHANNEL_AUTHENTICATOR, ChannelAuthenticator));

            return builder.ToString();
        }

        /// <summary>
        /// Loads options from serialized key-value lines
        /// </summary>
        public void Load(string serialized)
        {
            string[] lines = serialized.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] kv = line.Split(':');
                string key = kv[0].Trim();
                string value = kv[1].Trim();

                if (key.Equals(TmqHeaders.CONTENT_TYPE, StringComparison.InvariantCultureIgnoreCase))
                    Id = Convert.ToUInt16(value);

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

                if (key.Equals(TmqHeaders.ALLOW_MULTIPLE_QUEUES, StringComparison.InvariantCultureIgnoreCase))
                    AllowMultipleQueues = value == "1" || value == "true";

                if (key.Equals(TmqHeaders.ALLOWED_QUEUES, StringComparison.InvariantCultureIgnoreCase))
                {
                    string[] spx = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    AllowedQueues = new ushort[spx.Length];
                    for (int i = 0; i < spx.Length; i++)
                        AllowedQueues[i] = Convert.ToUInt16(spx[i]);
                }

                if (key.Equals(TmqHeaders.DESTROY_WHEN_EMPTY, StringComparison.InvariantCultureIgnoreCase))
                    DestroyWhenEmpty = value == "1" || value == "true";

                if (key.Equals(TmqHeaders.MESSAGE_LIMIT, StringComparison.InvariantCultureIgnoreCase))
                    MessageLimit = Convert.ToInt32(value);

                if (key.Equals(TmqHeaders.CLIENT_LIMIT, StringComparison.InvariantCultureIgnoreCase))
                    ClientLimit = Convert.ToInt32(value);

                if (key.Equals(TmqHeaders.QUEUE_LIMIT, StringComparison.InvariantCultureIgnoreCase))
                    QueueLimit = Convert.ToInt32(value);

                if (key.Equals(TmqHeaders.MESSAGE_DELIVERY_HANDLER, StringComparison.InvariantCultureIgnoreCase))
                    MessageDeliveryHandler = value;

                if (key.Equals(TmqHeaders.CHANNEL_EVENT_HANDLER, StringComparison.InvariantCultureIgnoreCase))
                    ChannelEventHandler = value;

                if (key.Equals(TmqHeaders.CHANNEL_AUTHENTICATOR, StringComparison.InvariantCultureIgnoreCase))
                    ChannelAuthenticator = value;

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

                        case "rr":
                        case "round":
                        case "robin":
                        case "roundrobin":
                        case "round-robin":
                            Status = QueueStatus.RoundRobin;
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

        #endregion

        #region Apply

        /// <summary>
        /// Applies non-null values to channel queue options
        /// </summary>
        public void ApplyToQueue(ChannelQueueOptions target)
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

        /// <summary>
        /// Applies non-null values to channel options
        /// </summary>
        public void ApplyToChannel(ChannelOptions target)
        {
            ApplyToQueue(target);

            if (AllowMultipleQueues.HasValue)
                target.AllowMultipleQueues = AllowMultipleQueues.Value;

            if (AllowedQueues != null)
                target.AllowedQueues = AllowedQueues;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates new key value line from boolean value.
        /// Value is stored as 0 or 1
        /// </summary>
        private static string Line(string key, bool value)
        {
            return key + ": " + (value ? "1" : "0") + "\r\n";
        }

        /// <summary>
        /// Creates new key value line from string value
        /// </summary>
        private static string Line(string key, string value)
        {
            return key + ": " + value + "\r\n";
        }

        /// <summary>
        /// Creates new key value line from TimeSpan value.
        /// Value is stored as numeric milliseconds
        /// </summary>
        private static string Line(string key, TimeSpan value)
        {
            int ms = Convert.ToInt32(value.TotalMilliseconds.ToString());
            return key + ": " + ms + "\r\n";
        }

        #endregion
    }
}