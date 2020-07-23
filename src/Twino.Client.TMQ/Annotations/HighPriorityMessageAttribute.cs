using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// The types have that attribute are send as high priority message to Twino MQ server
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HighPriorityMessageAttribute : Attribute
    {
    }
}