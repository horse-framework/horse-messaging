using System;

namespace Horse.Messaging.Client.Direct.Annotations
{
    /// <summary>
    /// Attribute to specify content type of direct and router messages
    /// Used for describing message type
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DirectContentTypeAttribute : Attribute
    {
        /// <summary>
        /// The Content Type for the type
        /// </summary>
        public ushort ContentType { get; }

        /// <summary>
        /// Creates new Content Type attribute
        /// </summary>
        public DirectContentTypeAttribute(ushort contentType)
        {
            ContentType = contentType;
        }
    }
}