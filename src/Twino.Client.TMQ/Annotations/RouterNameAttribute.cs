using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Router name attribute for router messages.
    /// Used for finding the router by name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RouterNameAttribute : Attribute
    {
        /// <summary>
        /// The router name for the type
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates new router name attribute
        /// </summary>
        public RouterNameAttribute(string name)
        {
            Name = name;
        }
    }
}