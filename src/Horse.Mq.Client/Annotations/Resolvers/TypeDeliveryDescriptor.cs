using System;
using System.Collections.Generic;
using Horse.Mq.Client.Models;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Annotations.Resolvers
{
    /// <summary>
    /// Type delivery descriptor for a type.
    /// Includes message properties
    /// </summary>
    public class TypeDeliveryDescriptor
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
        public MessagingQueueStatus? QueueStatus { get; set; }

        /// <summary>
        /// If queue is created with a message push and that value is not null, queue topic.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Headers for delivery descriptor of type
        /// </summary>
        public List<KeyValuePair<string, string>> Headers { get; }

        /// <summary>
        /// Content type for direct messages
        /// </summary>
        public ushort? ContentType { get; set; }

        /// <summary>
        /// Receiver finding method for direct messages
        /// </summary>
        public FindTargetBy DirectFindBy { get; set; }

        /// <summary>
        /// Direct message receiver value
        /// </summary>
        public string DirectValue { get; set; }

        /// <summary>
        /// Direct message full target
        /// </summary>
        public string DirectTarget { get; set; }

        /// <summary>
        /// Queue name for queue messages
        /// </summary>
        public string QueueName { get; set; }

        /// <summary>
        /// Router messages router name
        /// </summary>
        public string RouterName { get; set; }

        /// <summary>
        /// Delay between messages option (in milliseconds)
        /// </summary>
        public int? DelayBetweenMessages { get; set; }

        /// <summary>
        /// Put back delay in milliseconds
        /// </summary>
        public int? PutBackDelay { get; set; }

        /// <summary>
        /// True if type has QueueNameAttribute
        /// </summary>
        public bool HasQueueName { get; set; }

        /// <summary>
        /// True if type has ContentTypeAttribute
        /// </summary>
        public bool HasContentType { get; set; }

        /// <summary>
        /// True if type has RouterNameAttribute
        /// </summary>
        public bool HasRouterName { get; set; }

        /// <summary>
        /// True if type has DirectReceiverAttribute
        /// </summary>
        public bool HasDirectReceiver { get; set; }

        /// <summary>
        /// Message timeout in seconds
        /// </summary>
        public int? MessageTimeout { get; set; }

        /// <summary>
        /// Creates new type delivery descriptor
        /// </summary>
        public TypeDeliveryDescriptor()
        {
            Headers = new List<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Applies descriptor information to the message
        /// </summary>
        public HorseMessage CreateMessage(MessageType type, string overrideTargetName, ushort? overrideContentType)
        {
            string target = overrideTargetName;
            ushort? contentType = overrideContentType;

            switch (type)
            {
                case MessageType.QueueMessage:
                    if (string.IsNullOrEmpty(target))
                        target = QueueName;
                    break;

                case MessageType.DirectMessage:
                    if (string.IsNullOrEmpty(target))
                        target = DirectTarget;

                    if (!contentType.HasValue)
                        contentType = ContentType;
                    break;

                case MessageType.Router:
                    if (string.IsNullOrEmpty(target))
                        target = RouterName;

                    if (!contentType.HasValue)
                        contentType = ContentType;
                        
                    break;
            }

            HorseMessage message = new HorseMessage(type, target, contentType ?? 0);
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

            if (QueueStatus.HasValue)
                message.AddHeader(HorseHeaders.QUEUE_STATUS, QueueStatus.Value.ToString().Trim().ToLower());

            if (!string.IsNullOrEmpty(Topic))
                message.AddHeader(HorseHeaders.QUEUE_TOPIC, Topic);

            if (DelayBetweenMessages.HasValue)
                message.AddHeader(HorseHeaders.DELAY_BETWEEN_MESSAGES, DelayBetweenMessages.Value.ToString());

            if (PutBackDelay.HasValue)
                message.AddHeader(HorseHeaders.PUT_BACK_DELAY, PutBackDelay.Value.ToString());
            
            if (MessageTimeout.HasValue)
                message.AddHeader(HorseHeaders.MESSAGE_TIMEOUT, MessageTimeout.Value.ToString());

            foreach (KeyValuePair<string, string> pair in Headers)
                message.AddHeader(pair.Key, pair.Value);

            if (string.IsNullOrEmpty(target))
                throw new ArgumentNullException("Message target cannot be null");

            return message;
        }
    }
}