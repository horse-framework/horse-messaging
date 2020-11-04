using System;
using Twino.Client.Connectors;
using Twino.MQ.Client;
using Twino.MQ.Client.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Bus
{
    /// <summary>
    /// Wrapper class for event action.
    /// Used to prevent holding larger builder object in memory because of anonymous lambda function references
    /// </summary>
    internal class ExceptionEventMapper
    {
        private readonly TmqStickyConnector _connector;
        private readonly Action<Exception> _action;

        /// <summary>
        /// Creates new connection event wrapper
        /// </summary>
        public ExceptionEventMapper(TmqStickyConnector connector, Action<Exception> action)
        {
            _connector = connector;
            _action = action;
        }

        /// <summary>
        /// Event action mapper
        /// </summary>
        /// <returns></returns>
        public void Action(IConnector<TmqClient, TwinoMessage> c, Exception e)
        {
            _action(e);
        }
    }
}