using System;

namespace Horse.Mq.Client.Annotations
{
    /// <summary>
    /// The types have that attribute are send as high priority message to Horse MQ server
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HighPriorityMessageAttribute : Attribute
    {
    }
}