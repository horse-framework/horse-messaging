using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Direct
{
    internal class DirectHandlerRegistration
    {
        /// <summary>
        /// Subscribed content type
        /// </summary>
        public ushort ContentType { get; set; }

        /// <summary>
        /// Direct Consumer type
        /// </summary>
        public Type ConsumerType { get; set; }

        /// <summary>
        /// Direct message type
        /// </summary>
        public Type MessageType { get; set; }

        /// <summary>
        /// Request handler's response type
        /// </summary>
        public Type ResponseType { get; set; }

        /// <summary>
        /// Interceptor descriptors
        /// </summary>
        public List<InterceptorTypeDescriptor> IntercetorDescriptors { get; } = new();
        
        /// <summary>
        /// Consumer executer
        /// </summary>
        internal ExecuterBase ConsumerExecuter { get; set; }


    }
}