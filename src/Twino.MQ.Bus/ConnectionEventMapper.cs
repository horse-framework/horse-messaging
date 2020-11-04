using System;
using Twino.MQ.Client;
using Twino.MQ.Client.Connectors;

namespace Twino.MQ.Bus
{
    /// <summary>
    /// Mapper class for event action.
    /// Used to prevent holding larger builder object in memory because of anonymous lambda function references
    /// </summary>
    internal class ConnectionEventMapper
    {
        private readonly TmqStickyConnector _connector;
        private readonly Action<TmqStickyConnector> _action;

        /// <summary>
        /// Creates new connection event wrapper
        /// </summary>
        public ConnectionEventMapper(TmqStickyConnector connector, Action<TmqStickyConnector> action)
        {
            _connector = connector;
            _action = action;
        }

        /// <summary>
        /// Event action mapper
        /// </summary>
        /// <returns></returns>
        public void Action(TmqClient client)
        {
            _action(_connector);
        }
    }
}