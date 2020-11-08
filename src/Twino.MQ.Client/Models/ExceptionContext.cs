using System;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client.Models
{
    /// <summary>
    /// Used when an exception is ready to push or publish
    /// </summary>
    public class ExceptionContext
    {
        /// <summary>
        /// Consumer object
        /// </summary>
        public object Consumer { get; set; }
        
        /// <summary>
        /// The consuming message when exception is thrown
        /// </summary>
        public TwinoMessage ConsumingMessage { get; set; }
        
        /// <summary>
        /// Exception object
        /// </summary>
        public Exception Exception { get; set; }
    }
}