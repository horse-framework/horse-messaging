using System;

namespace Twino.SocketModels.Models
{
    /// <summary>
    /// Used for define waiting request type to response by Request Manager.
    /// </summary>
    internal class RequestDescriptor
    {
        /// <summary>
        /// Request type code
        /// </summary>
        public int RequestNo { get; set; }
        
        /// <summary>
        /// Request model type
        /// </summary>
        public Type RequestType { get; set; }
        
        /// <summary>
        /// Response type code
        /// </summary>
        public int ResponseNo { get; set; }
        
        /// <summary>
        /// Response model type
        /// </summary>
        public Type ResponseType { get; set; }
        
        /// <summary>
        /// The method will be called when the request is received to create a response
        /// </summary>
        public Delegate Action { get; set; }
    }
}