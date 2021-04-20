using System;

namespace Horse.Messaging.Client.Routers.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RouterContentTypeAttribute : Attribute
    {
        public ushort ContentType { get; }

        public RouterContentTypeAttribute(ushort contentType)
        {
            ContentType = contentType;
        }
    }
}