using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Client.Connectors;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Network
{
    internal class NodeMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseMq _server;

        /// <summary>
        /// Default HMQ protocol message writer
        /// </summary>
        private static readonly HmqWriter _writer = new HmqWriter();

        public NodeMessageHandler(HorseMq server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, HorseMessage message, bool fromNode)
        {
            //if server is not set or there is no connected server
            if (_server.NodeManager.OutgoingNodes.Length == 0)
                return;

            byte[] mdata = HmqWriter.Create(message);
            foreach (OutgoingNode node in _server.NodeManager.OutgoingNodes)
            {
                if (node?.Connector == null)
                    continue;

                HmqStickyConnector connector = node.Connector;

                bool grant = _server.NodeManager.Authenticator == null || await _server.NodeManager.Authenticator.CanReceive(connector.GetClient(), message);

                if (grant)
                    _ = connector.SendAsync(mdata);
            }
        }
    }
}