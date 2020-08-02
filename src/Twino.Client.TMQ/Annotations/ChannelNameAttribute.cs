using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Channel Name attribute for queue messages.
    /// Used for finding the channels for types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ChannelNameAttribute : Attribute
    {
        /// <summary>
        /// The channel name for the type
        /// </summary>
        public string Channel { get; }

        /// <summary>
        /// Creates new channel name attribute
        /// </summary>
        public ChannelNameAttribute(string channel)
        {
            Channel = channel;
        }
    }
}