using System;

namespace Twino.MQ.Client.Annotations
{
    /// <summary>
    /// Used for models that are sent as direct messages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DirectTargetAttribute : Attribute
    {
        /// <summary>
        /// Finding method for message target
        /// </summary>
        public FindTargetBy FindBy { get; }

        /// <summary>
        /// The value is Id, Type or Name depends on FindBy value 
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Creates new Direct Receiver Attribute
        /// </summary>
        public DirectTargetAttribute(FindTargetBy findBy, string value)
        {
            FindBy = findBy;
            Value = value;
        }
    }
}