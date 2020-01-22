using System;
using System.Collections.Generic;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Options
{
    /// <summary>
    /// Queue options
    /// </summary>
    public class ChannelQueueOptions
    {
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
        public TimeSpan AcknowledgeTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        public TimeSpan MessageTimeout { get; set; } = TimeSpan.Zero;

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

        /// <summary>
        /// Default status for the queue
        /// </summary>
        public QueueStatus Status { get; set; } = QueueStatus.Route;

        /// <summary>
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        public int MessageLimit { get; set; }

        /// <summary>
        /// Creates clone of the object
        /// </summary>
        /// <returns></returns>
        internal object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// Fills value from properties
        /// </summary>
        internal void FillFromProperties(Dictionary<string, string> properties)
        {
            foreach (KeyValuePair<string, string> pair in properties)
            {
                if (pair.Value.Equals(TmqHeaders.ONLY_FIRST_ACQUIRER, StringComparison.InvariantCultureIgnoreCase))
                    SendOnlyFirstAcquirer = pair.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || pair.Value == "1";

                else if (pair.Value.Equals(TmqHeaders.REQUEST_ACKNOWLEDGE, StringComparison.InvariantCultureIgnoreCase))
                    RequestAcknowledge = pair.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || pair.Value == "1";

                else if (pair.Value.Equals(TmqHeaders.USE_MESSAGE_ID, StringComparison.InvariantCultureIgnoreCase))
                    UseMessageId = pair.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || pair.Value == "1";

                else if (pair.Value.Equals(TmqHeaders.WAIT_FOR_ACKNOWLEDGE, StringComparison.InvariantCultureIgnoreCase))
                    WaitForAcknowledge = pair.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || pair.Value == "1";

                else if (pair.Value.Equals(TmqHeaders.HIDE_CLIENT_NAMES, StringComparison.InvariantCultureIgnoreCase))
                    HideClientNames = pair.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || pair.Value == "1";

                else if (pair.Value.Equals(TmqHeaders.ACKNOWLEDGE_TIMEOUT, StringComparison.InvariantCultureIgnoreCase))
                    AcknowledgeTimeout = TimeSpan.FromMilliseconds(Convert.ToInt32(pair.Value));

                else if (pair.Value.Equals(TmqHeaders.MESSAGE_TIMEOUT, StringComparison.InvariantCultureIgnoreCase))
                    MessageTimeout = TimeSpan.FromMilliseconds(Convert.ToInt32(pair.Value));

                else if (pair.Value.Equals(TmqHeaders.MESSAGE_LIMIT, StringComparison.InvariantCultureIgnoreCase))
                    MessageLimit = Convert.ToInt32(pair.Value);

                else if (pair.Value.Equals(TmqHeaders.QUEUE_STATUS, StringComparison.InvariantCultureIgnoreCase))
                {
                    switch (pair.Value.ToLower())
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

        /// <summary>
        /// Clones channel queue options from another options
        /// </summary>
        internal static ChannelQueueOptions CloneFrom(ChannelQueueOptions options)
        {
            return new ChannelQueueOptions
                   {
                       Status = options.Status,
                       AcknowledgeTimeout = options.AcknowledgeTimeout,
                       MessageTimeout = options.MessageTimeout,
                       RequestAcknowledge = options.RequestAcknowledge,
                       HideClientNames = options.HideClientNames,
                       UseMessageId = options.UseMessageId,
                       WaitForAcknowledge = options.WaitForAcknowledge,
                       SendOnlyFirstAcquirer = options.SendOnlyFirstAcquirer,
                       MessageLimit = options.MessageLimit
                   };
        }
    }
}