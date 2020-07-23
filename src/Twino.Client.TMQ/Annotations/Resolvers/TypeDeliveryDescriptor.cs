using System;
using System.Collections.Generic;

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
        /// Headers for delivery descriptor of type
        /// </summary>
        public List<KeyValuePair<string, string>> Headers { get; }

        /// <summary>
        /// Content type for direct messages
        /// </summary>
        public ushort? DirectContentType { get; set; }

        /// <summary>
        /// Receiver finding method for direct messages
        /// </summary>
        public FindReceiverBy DirectFindBy { get; set; }

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
        /// Creates new type delivery descriptor
        /// </summary>
        public TypeDeliveryDescriptor()
        {
            Headers = new List<KeyValuePair<string, string>>();
        }
    }
}