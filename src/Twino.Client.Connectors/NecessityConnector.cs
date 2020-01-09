using System;
using Twino.Core;

namespace Twino.Client.Connectors
{
    /// <summary>
    /// Connects to the server, when Send method is called.
    /// After message is sent, connection keeps alive.
    /// Next messages are sent via this connection.
    /// But if the connection is closed because of some reason.
    /// Does not connect until next Send method is called.
    /// </summary>
    public class NecessityConnector<TClient, TMessage> : ConnectorBase<TClient, TMessage>
        where TClient : ClientSocketBase<TMessage>, new()
    {
        /// <summary>
        /// Creates new necessity connector
        /// </summary>
        public NecessityConnector(Func<TClient> createInstance)
            : base(createInstance)
        {
        }

        /// <summary>
        /// Creates new necessity connector
        /// </summary>
        public NecessityConnector()
        {
        }

        /// <summary>
        /// Disconnects from the server
        /// </summary>
        public override void Abort()
        {
            Disconnect();
        }

        /// <summary>
        /// Connects to the server. Run method call is not must.
        /// If this method is not called, first connection will be established with first message send.
        /// </summary>
        public override void Run()
        {
            Connect();
        }

        /// <summary>
        /// If disconnected, connects and sends the message.
        /// If connected, only sends the message.
        /// </summary>
        public override bool Send(byte[] preparedData)
        {
            if (NeedReconnect())
            {
                try
                {
                    Connect();
                }
                catch (Exception ex)
                {
                    RaiseException(ex);
                    return false;
                }
            }

            return base.Send(preparedData);
        }
    }
}