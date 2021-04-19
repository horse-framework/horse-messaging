using System;

namespace Horse.Messaging.Client.Events.Annotations
{
    /// <summary>
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HorseEventAttribute : Attribute
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