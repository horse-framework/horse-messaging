using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Used for models that are sent as direct messages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DirectReceiverAttribute : Attribute
    {
        /// <summary>
        /// Finding method for message target
        /// </summary>
        public FindReceiverBy FindBy { get; }

        /// <summary>
        /// The value is Id, Type or Name depends on FindBy value 
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Creates new Direct Receiver Attribute
        /// </summary>
        public DirectReceiverAttribute(FindReceiverBy findBy, string value)
        {
            FindBy = findBy;
            Value = value;
        }
    }
}