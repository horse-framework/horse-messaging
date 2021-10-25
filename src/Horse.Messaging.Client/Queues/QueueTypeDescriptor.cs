using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues
{
    /// <summary>
    /// Type descriptor for queue messages
    /// </summary>
    public class QueueTypeDescriptor : ITypeDescriptor
    {
        /// <summary>
        /// Message model type
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// If true, message is sent as high priority
        /// </summary>
        public bool HighPriority { get; set; }

        /// <summary>
        /// If queue is created with a message push and that value is not null, that option will be used
        /// </summary>
        public QueueAckDecision? Acknowledge { get; set; }

        /// <summary>
        /// If queue is created with a message push and that value is not null, queue will be created with that status
        /// </summary>
        public MessagingQueueType? QueueType { get; set; }

        /// <summary>
        /// If queue is created with a message push and that value is not null, queue topic.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Headers for delivery descriptor of type
        /// </summary>
        public List<KeyValuePair<string, string>> Headers { get; }

        /// <summary>
        /// Queue name for queue messages
        /// </summary>
        public string QueueName { get; set; }

        /// <summary>
        /// Delay between messages option (in milliseconds)
        /// </summary>
        public int? DelayBetweenMessages { get; set; }

        /// <summary>
        /// Put back decision
        /// </summary>
        public PutBack? PutBackDecision { get; set; }

        /// <summary>
        /// Put back delay in milliseconds
        /// </summary>
        public int? PutBackDelay { get; set; }

        /// <summary>
        /// True if type has QueueNameAttribute
        /// </summary>
        public bool HasQueueName { get; set; }

        /// <summary>
        /// Message timeout in seconds
        /// </summary>
        public int? MessageTimeout { get; set; }

        /// <summary>
        /// Acknowledge timeout in seconds
        /// </summary>
        public int? AcknowledgeTimeout { get; set; }

        /// <summary>
        /// Creates new type delivery descriptor
        /// </summary>
        public QueueTypeDescriptor()
        {
            Headers = new List<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Applies descriptor information to the message
        /// </summary>
        public HorseMessage CreateMessage()
        {
            HorseMessage message = new HorseMessage(MessageType.QueueMessage, QueueName, 0);
            if (HighPriority)
                message.HighPriority = HighPriority;

            if (Acknowledge.HasValue)
            {
                switch (Acknowledge.Value)
                {
                    case QueueAckDecision.None:
                        message.AddHeader(HorseHeaders.ACKNOWLEDGE, "none");
                        break;

                    case QueueAckDecision.JustRequest:
                        message.AddHeader(HorseHeaders.ACKNOWLEDGE, "request");
                        break;

                    case QueueAckDecision.WaitForAcknowledge:
                        message.AddHeader(HorseHeaders.ACKNOWLEDGE, "wait");
                        break;
                }
            }

            if (HasQueueName)
                message.AddHeader(HorseHeaders.QUEUE_NAME, QueueName);

            if (QueueType.HasValue)
                message.AddHeader(HorseHeaders.QUEUE_TYPE, QueueType.Value.ToString().Trim().ToLower());

            if (!string.IsNullOrEmpty(Topic))
                message.AddHeader(HorseHeaders.QUEUE_TOPIC, Topic);

            if (DelayBetweenMessages.HasValue)
                message.AddHeader(HorseHeaders.DELAY_BETWEEN_MESSAGES, DelayBetweenMessages.Value.ToString());

            if (PutBackDecision.HasValue)
                message.AddHeader(HorseHeaders.PUT_BACK, PutBackDecision.Value.ToString());
            
            if (PutBackDelay.HasValue)
                message.AddHeader(HorseHeaders.PUT_BACK_DELAY, PutBackDelay.Value.ToString());

            if (MessageTimeout.HasValue)
                message.AddHeader(HorseHeaders.MESSAGE_TIMEOUT, MessageTimeout.Value.ToString());

            if (AcknowledgeTimeout.HasValue)
                message.AddHeader(HorseHeaders.ACK_TIMEOUT, AcknowledgeTimeout.Value.ToString());

            foreach (KeyValuePair<string, string> pair in Headers)
                message.AddHeader(pair.Key, pair.Value);

            return message;
        }
    }
}