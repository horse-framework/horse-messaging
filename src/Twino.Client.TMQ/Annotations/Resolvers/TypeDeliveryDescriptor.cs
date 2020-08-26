using System;
using System.Collections.Generic;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Annotations.Resolvers
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
        /// If true, message is consumed by only first consumer
        /// </summary>
        public bool OnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If queue is created with a message push and that value is not null, wait for acknowledge option will be used
        /// </summary>
        public bool? WaitForAcknowledge { get; set; }

        /// <summary>
        /// If queue is created with a message push and that value is not null, queue will be created with that status
        /// </summary>
        public MessagingQueueStatus? QueueStatus { get; set; }

        /// <summary>
        /// If queue is created with a message push and that value is not null, queue will be created with that Tag
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// If channel is created with a message push and that value is not null, channel topic.
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
        /// Queue messages channel name
        /// </summary>
        public string ChannelName { get; set; }

        /// <summary>
        /// Queue messages queue Id
        /// </summary>
        public ushort? QueueId { get; set; }

        /// <summary>
        /// Router messages router name
        /// </summary>
        public string RouterName { get; set; }

        /// <summary>
        /// True if type has ChannelNameAttribute
        /// </summary>
        public bool HasChannelName { get; set; }

        /// <summary>
        /// True if type has QueueIdAttribute
        /// </summary>
        public bool HasQueueId { get; set; }

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
        /// Creates new type delivery descriptor
        /// </summary>
        public TypeDeliveryDescriptor()
        {
            Headers = new List<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Applies descriptor information to the message
        /// </summary>
        public TwinoMessage CreateMessage(MessageType type, string overrideTargetName, ushort? overrideContentType)
        {
            string target = overrideTargetName;
            ushort? contentType = overrideContentType;

            switch (type)
            {
                case MessageType.QueueMessage:
                    if (string.IsNullOrEmpty(target))
                        target = ChannelName;

                    if (!contentType.HasValue)
                        contentType = QueueId;
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

            TwinoMessage message = new TwinoMessage(type, target, contentType ?? 0);
            if (HighPriority)
                message.HighPriority = HighPriority;

            if (OnlyFirstAcquirer)
                message.FirstAcquirer = true;

            if (WaitForAcknowledge.HasValue)
                message.AddHeader(TmqHeaders.WAIT_FOR_ACKNOWLEDGE, WaitForAcknowledge.Value ? "1" : "0");
            
            if (QueueStatus.HasValue)
                message.AddHeader(TmqHeaders.QUEUE_STATUS, QueueStatus.Value.ToString().ToLower());

            if (!string.IsNullOrEmpty(Tag))
                message.AddHeader(TmqHeaders.QUEUE_TAG, Tag);

            if (!string.IsNullOrEmpty(Topic))
                message.AddHeader(TmqHeaders.CHANNEL_TOPIC, Topic);

            foreach (KeyValuePair<string, string> pair in Headers)
                message.AddHeader(pair.Key, pair.Value);

            if (string.IsNullOrEmpty(target))
                throw new ArgumentNullException("Message target cannot be null");

            return message;
        }
    }
}