using System;

namespace Horse.Messaging.Client.Channels.Annotations
{
    /// <summary>
    /// Channel name attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ChannelNameAttribute : Attribute
    {
        /// <summary>
        /// Name of the channel
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates new channel nama attribute
        /// </summary>
        public ChannelNameAttribute(string name)
        {
            Name = name;
        }
    }
}