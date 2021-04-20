using System;

namespace Horse.Messaging.Client.Routers.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RouterTopicAttribute : Attribute
    {
        public string Topic { get; }

        public RouterTopicAttribute(string topic)
        {
            Topic = topic;
        }
    }
}