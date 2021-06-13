using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct
{
    /// <summary>
    /// Type descriptor for direct messages
    /// </summary>
    public class DirectTypeDescriptor : ITypeDescriptor
    {
        /// <summary>
        /// Message model type
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// If true, message is sent as high priority
        /// </summary>
        public bool? HighPriority { get; set; }

        /// <summary>
        /// Headers for delivery descriptor of type
        /// </summary>
        public List<KeyValuePair<string, string>> Headers { get; } = new List<KeyValuePair<string, string>>();

        /// <summary>
        /// Content type for direct messages
        /// </summary>
        public ushort? ContentType { get; set; }

        /// <summary>
        /// Receiver finding method for direct messages
        /// </summary>
        public FindTargetBy FindBy { get; set; }

        /// <summary>
        /// Direct message receiver value
        /// </summary>
        public string DirectValue { get; set; }

        /// <summary>
        /// Direct message full target
        /// </summary>
        public string DirectTarget { get; set; }

    
        internal bool DirectTargetSpecified { get; set; }

        internal List<Func<KeyValuePair<string, string>>> HeaderFactories { get; } = new List<Func<KeyValuePair<string, string>>>();
        internal Func<Type, string> DirectTargetFactory { get; private set; }

        /// <summary>
        /// Creates new direct message
        /// </summary>
        public HorseMessage CreateMessage()
        {
            if (string.IsNullOrEmpty(DirectTarget))
                throw new ArgumentNullException("Message target cannot be null");

            HorseMessage message = new HorseMessage(MessageType.DirectMessage, DirectTarget, ContentType ?? 0);
            if (HighPriority.HasValue)
                message.HighPriority = HighPriority.Value;

            foreach (KeyValuePair<string, string> pair in Headers)
                message.AddHeader(pair.Key, pair.Value);

            return message;
        }
    }
}