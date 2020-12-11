using System;
using Horse.Client.Connectors;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Bus
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
        public void Action(IConnector<HorseClient, HorseMessage> c, Exception e)
        {
            _action(e);
        }
    }
}