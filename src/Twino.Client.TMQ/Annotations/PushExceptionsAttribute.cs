using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Pushes exceptions to queues when thrown by consumer objects
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PushExceptionsAttribute : Attribute
    {
        /// <summary>
        /// Exception type
        /// </summary>
        public Type ExceptionType { get; }
        
        /// <summary>
        /// Channel name
        /// </summary>
        public string ChannelName { get; }
        
        /// <summary>
        /// Queue Id
        /// </summary>
        public ushort QueueId { get; }

        /// <summary>
        /// Pushes all exceptions
        /// </summary>
        public PushExceptionsAttribute(string channel, ushort queue)
        {
            ChannelName = channel;
            QueueId = queue;
        }

        /// <summary>
        /// Pushes specified type of exceptions
        /// </summary>
        public PushExceptionsAttribute(Type exceptionType, string channel, ushort queue)
        {
            ExceptionType = exceptionType;
            ChannelName = channel;
            QueueId = queue;
        }
    }
}