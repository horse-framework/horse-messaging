using System;

namespace Horse.Messaging.Client.Routers.Annotations
{
    /// <summary>
    /// Describes content type for target router
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RouterContentTypeAttribute : Attribute
    {
        /// <summary>
        /// Message content type
        /// </summary>
        public ushort ContentType { get; }

        /// <summary>
        /// Creates new router content type attribute
        /// </summary>
        public RouterContentTypeAttribute(ushort contentType)
        {
            ContentType = contentType;
        }
    }
}