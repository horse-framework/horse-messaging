using System;
using Horse.Messaging.Protocol.Events;

namespace Horse.Messaging.Client.Events.Annotations
{
    /// <summary>
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal class HorseEventAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        public HorseEventType EventType { get; }

        /// <summary>
        /// 
        /// </summary>
        public HorseEventAttribute(HorseEventType eventType)
        {
            EventType = eventType;
        }
    }
}