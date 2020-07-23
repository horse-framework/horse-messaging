using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Attribute to specify content type of direct messages
    /// Used for describing message type
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ContentTypeAttribute : Attribute
    {
        /// <summary>
        /// The Content Type for the type
        /// </summary>
        public ushort ContentType { get; }

        /// <summary>
        /// Creates new Content Type attribute
        /// </summary>
        public ContentTypeAttribute(ushort contentType)
        {
            ContentType = contentType;
        }
    }
}