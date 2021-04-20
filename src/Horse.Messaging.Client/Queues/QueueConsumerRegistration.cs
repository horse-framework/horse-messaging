using System;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Queues
{
    internal class QueueConsumerRegistration
    {
        /// <summary>
        /// Queue name
        /// </summary>
        public string QueueName { get; set; }
        
        /// <summary>
        /// Direct Consumer type
        /// </summary>
        public Type ConsumerType { get; set; }

        /// <summary>
        /// Direct message type
        /// </summary>
        public Type MessageType { get; set; }

        /// <summary>
        /// Consumer executer
        /// </summary>
        internal ExecuterBase ConsumerExecuter { get; set; }
    }
}