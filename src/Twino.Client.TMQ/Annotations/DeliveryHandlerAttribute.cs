using System;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Used to add delivery handler key header to message.
    /// It's useful if queue is not exist and will be created with first push,
    /// Server delivery handler builder can use that value
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class DeliveryHandlerAttribute : MessageHeaderAttribute
    {
        /// <summary>
        /// Creates new Delivery Handler Attribute
        /// </summary>
        public DeliveryHandlerAttribute(string value) : base(TwinoHeaders.DELIVERY_HANDLER, value)
        {
        }
    }
}