using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Used when queue is created with first push
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueTagAttribute : Attribute
    {
        /// <summary>
        /// The Queue Tag for the type
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// Creates new Queue Tag attribute
        /// </summary>
        public QueueTagAttribute(string tag)
        {
            Tag = tag;
        }
    }
}