using System.Threading.Tasks;
using Twino.Client.TMQ.Connectors;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class NodeMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        /// <summary>
        /// Default TMQ protocol message writer
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

        public NodeMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            //if server is not set or there is no connected server
            if (_server.InstanceManager.Connectors.Length == 0)
                return;

            byte[] mdata = TmqWriter.Create(message);
            foreach (TmqStickyConnector connector in _server.InstanceManager.Connectors)
            {
                bool grant = _server.InstanceManager.Authenticator == null || await _server.InstanceManager.Authenticator.CanReceive(connector.GetClient(), message);

                if (grant)
                    _ = connector.SendAsync(mdata);
            }
        }
    }
}