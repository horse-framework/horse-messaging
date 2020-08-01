using System;
using System.Collections.Generic;
using Twino.MQ.Queues;

namespace Twino.MQ
{
    /// <summary>
    /// Helper parameter for delivery handler factory implementation
    /// </summary>
    public class DeliveryHandlerBuilder
    {
        /// <summary>
        /// Twino MQ Server
        /// </summary>
        public TwinoMQ Server { get; internal set; }
        
        /// <summary>
        /// Channel of the queue
        /// </summary>
        public Channel Channel { get; internal set; }
        
        /// <summary>
        /// The queue that will use delivery handler
        /// </summary>
        public ChannelQueue Queue { get; internal set; }

        /// <summary>
        /// Header information for delivery handler.
        /// The value of "Delivery-Handler" key.
        /// Used when the factory method is triggered over network by a client.
        /// </summary>
        public string DeliveryHandlerHeader { get; set; }
        
        /// <summary>
        /// All header data of the message that is received over network from a client.
        /// </summary>
        public IEnumerable<KeyValuePair<string,string>> Headers { get; internal set; }

        private Action<DeliveryHandlerBuilder> _afterCompleted;
        
        internal DeliveryHandlerBuilder()
        {
        }

        /// <summary>
        /// Subscribes to after delivery handler and queue created operation
        /// </summary>
        public void OnAfterCompleted(Action<DeliveryHandlerBuilder> action)
        {
            _afterCompleted = action;
        }

        internal void TriggerAfterCompleted()
        {
            if (_afterCompleted != null)
                _afterCompleted(this);
        }
        
    }
}