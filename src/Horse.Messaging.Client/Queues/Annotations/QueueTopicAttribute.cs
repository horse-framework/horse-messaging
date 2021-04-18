using System;

namespace Horse.Messaging.Client.Annotations
{
    /// <summary>
    /// Queue Topic attribute for queue messages
    /// </summary>
    public class QueueTopicAttribute : Attribute
    {
        /// <summary>
        /// The queue topic for the type
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Creates new queue topic attribute
        /// </summary>
        public QueueTopicAttribute(string topic)
        {
            Topic = topic;
        }
    }
}