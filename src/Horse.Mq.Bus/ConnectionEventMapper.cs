using System;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;

namespace Horse.Mq.Bus
{
    /// <summary>
    /// Mapper class for event action.
    /// Used to prevent holding larger builder object in memory because of anonymous lambda function references
    /// </summary>
    internal class ConnectionEventMapper
    {
        private readonly HmqStickyConnector _connector;
        private readonly Action<HmqStickyConnector> _action;

        /// <summary>
        /// Creates new connection event wrapper
        /// </summary>
        public ConnectionEventMapper(HmqStickyConnector connector, Action<HmqStickyConnector> action)
        {
            _connector = connector;
            _action = action;
        }

        /// <summary>
        /// Event action mapper
        /// </summary>
        /// <returns></returns>
        public void Action(HorseClient client)
        {
            _action(_connector);
        }
    }
}