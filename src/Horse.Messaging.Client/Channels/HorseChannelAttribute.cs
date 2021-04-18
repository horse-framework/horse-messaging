using System;

namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Horse channel name attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HorseChannelAttribute : Attribute
    {
        /// <summary>
        /// Channel name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates new horse channel attribute
        /// </summary>
        public HorseChannelAttribute(string name)
        {
            Name = name;
        }
    }
}