using System;

namespace Twino.MQ.Client.Annotations
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
        /// Queue name
        /// </summary>
        public string QueueName { get; }
        
        /// <summary>
        /// Pushes all exceptions
        /// </summary>
        public PushExceptionsAttribute(string queue)
        {
            QueueName = queue;
        }

        /// <summary>
        /// Pushes specified type of exceptions
        /// </summary>
        public PushExceptionsAttribute(Type exceptionType, string queue)
        {
            ExceptionType = exceptionType;
            QueueName = queue;
        }
    }
}