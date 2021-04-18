using System;
using Horse.Client.Connectors;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Mq.Client.Connectors;

namespace Horse.Messaging.Bus
{
    /// <summary>
    /// Wrapper class for event action.
    /// Used to prevent holding larger builder object in memory because of anonymous lambda function references
    /// </summary>
    internal class ExceptionEventMapper
    {
        private readonly HmqStickyConnector _connector;
        private readonly Action<Exception> _action;

        /// <summary>
        /// Creates new connection event wrapper
        /// </summary>
        public ExceptionEventMapper(HmqStickyConnector connector, Action<Exception> action)
        {
            _connector = connector;
            _action = action;
        }

        /// <summary>
        /// Event action mapper
        /// </summary>
        /// <returns></returns>
        public void Action(HorseClient c, Exception e)
        {
            _action(e);
        }
    }
}