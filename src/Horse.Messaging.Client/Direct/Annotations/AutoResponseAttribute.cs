using System;

namespace Horse.Messaging.Client.Direct.Annotations
{
    /// <summary>
    /// Auto response for direct message handlers
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoResponseAttribute : Attribute
    {
        /// <summary>
        /// Decides when message will be respond
        /// </summary>
        public AutoResponse Response { get; }
        
        /// <summary>
        /// Error type for failed operations
        /// </summary>
        public ErrorResponseType Error { get; }

        /// <summary>
        /// Creates new auto response attribute
        /// </summary>
        public AutoResponseAttribute(AutoResponse response, ErrorResponseType error = ErrorResponseType.None)
        {
            Response = response;
            Error = error;
        }
    }
}