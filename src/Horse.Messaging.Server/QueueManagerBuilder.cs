using System;
using System.Collections.Generic;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Helper parameter for horse queue manager factory implementation
    /// </summary>
    public class QueueManagerBuilder
    {
        /// <summary>
        /// Horse MQ Server
        /// </summary>
        public HorseRider Server { get; internal set; }
        
        /// <summary>
        /// The queue that will use delivery handler
        /// </summary>
        public HorseQueue Queue { get; internal set; }

        /// <summary>
        /// Queue manager name.
        /// Used when the factory method is triggered over network by a client.
        /// </summary>
        public string ManagerName { get; set; }
        
        /// <summary>
        /// All header data of the message that is received over network from a client.
        /// </summary>
        public IEnumerable<KeyValuePair<string,string>> Headers { get; internal set; }

        private Action<QueueManagerBuilder> _afterCompleted;
        
        internal QueueManagerBuilder()
        {
        }

        /// <summary>
        /// Subscribes to after delivery handler and queue created operation
        /// </summary>
        public void OnAfterCompleted(Action<QueueManagerBuilder> action)
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