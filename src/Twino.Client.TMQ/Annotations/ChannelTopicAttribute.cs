using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Channel Topic attribute for queue messages.
    /// Used for finding the channels by topics.
    /// </summary>
    public class ChannelTopicAttribute : Attribute
    {
        /// <summary>
        /// The channel topic for the type
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Creates new channel topic attribute
        /// </summary>
        public ChannelTopicAttribute(string topic)
        {
            Topic = topic;
        }
    }
}