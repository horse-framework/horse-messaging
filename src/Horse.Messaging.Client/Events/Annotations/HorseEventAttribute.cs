using System;

namespace Horse.Messaging.Client.Events.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HorseEventAttribute : Attribute
    {
        public HorseEventType EventType { get; }

        public HorseEventAttribute(HorseEventType eventType)
        {
            EventType = eventType;
        }
    }
}