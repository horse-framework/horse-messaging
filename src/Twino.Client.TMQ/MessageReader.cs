using System;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// MessageReader name is changed to MessageConsumer.
    /// Use MessageConsumer directly
    /// </summary>
    [Obsolete("Use MessageConsumer istead")]
    public class MessageReader : MessageObserver
    {
        /// <summary>
        /// MessageReader name is changed to MessageConsumer.
        /// Use MessageConsumer directly
        /// </summary>
        public MessageReader(Func<TmqMessage, Type, object> func) : base(func)
        {
        }
    }
}