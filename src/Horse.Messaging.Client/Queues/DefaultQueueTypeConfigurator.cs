using System;
using System.Collections.Generic;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues
{
    /// <summary>
    /// Configurator for default queue type descriptor
    /// </summary>
    public class DefaultQueueTypeConfigurator
    {
        /// <summary>
        /// Message timeout in seconds
        /// </summary>
        public TimeSpan? MessageTimeout
        {
            get => TimeSpan.FromSeconds(_operator.DescriptorContainer.Default.MessageTimeout ?? 0);
            set => _operator.DescriptorContainer.Default.MessageTimeout = value.HasValue ? Convert.ToInt32(value.Value.TotalSeconds) : 0;
        }

        /// <summary>
        /// Acknowledge timeout
        /// </summary>
        public TimeSpan? AcknowledgeTimeout
        {
            get => TimeSpan.FromSeconds(_operator.DescriptorContainer.Default.AcknowledgeTimeout ?? 0);
            set => _operator.DescriptorContainer.Default.AcknowledgeTimeout = value.HasValue ? Convert.ToInt32(value.Value.TotalSeconds) : 0;
        }

        /// <summary>
        /// Delay between messages option
        /// </summary>
        public TimeSpan? DelayBetweenMessages
        {
            get => TimeSpan.FromMilliseconds(_operator.DescriptorContainer.Default.DelayBetweenMessages ?? 0);
            set => _operator.DescriptorContainer.Default.DelayBetweenMessages = value.HasValue ? Convert.ToInt32(value.Value.TotalMilliseconds) : 0;
        }

        /// <summary>
        /// Put back delay
        /// </summary>
        public TimeSpan? PutBackDelay
        {
            get => TimeSpan.FromMilliseconds(_operator.DescriptorContainer.Default.PutBackDelay ?? 0);
            set => _operator.DescriptorContainer.Default.PutBackDelay = value.HasValue ? Convert.ToInt32(value.Value.TotalMilliseconds) : 0;
        }

        /// <summary>
        /// If true, message is sent as high priority
        /// </summary>
        public bool HighPriority
        {
            get => _operator.DescriptorContainer.Default.HighPriority;
            set => _operator.DescriptorContainer.Default.HighPriority = value;
        }

        /// <summary>
        /// If queue is created with a message push and that value is not null, that option will be used
        /// </summary>
        public QueueAckDecision? Acknowledge
        {
            get => _operator.DescriptorContainer.Default.Acknowledge;
            set => _operator.DescriptorContainer.Default.Acknowledge = value;
        }

        /// <summary>
        /// If queue is created with a message push and that value is not null, queue will be created with that status
        /// </summary>
        public MessagingQueueType? QueueType
        {
            get => _operator.DescriptorContainer.Default.QueueType;
            set => _operator.DescriptorContainer.Default.QueueType = value;
        }

        /// <summary>
        /// If queue is created with a message push and that value is not null, queue topic.
        /// </summary>
        public string Topic
        {
            get => _operator.DescriptorContainer.Default.Topic;
            set => _operator.DescriptorContainer.Default.Topic = value;
        }

        private readonly QueueOperator _operator;

        internal DefaultQueueTypeConfigurator(QueueOperator queueOperator)
        {
            _operator = queueOperator;
        }

        /// <summary>
        /// Adds new header
        /// </summary>
        public void AddHeader(string key, string value)
        {
            _operator.DescriptorContainer.Default.Headers.Add(new KeyValuePair<string, string>(key, value));
        }
    }
}