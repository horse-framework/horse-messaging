using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Network
{
    internal class NodeMessageHandler : INetworkMessageHandler
    {
        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseMq _server;

        public NodeMessageHandler(HorseMq server)
        {
            _server = server;
        }

        public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            //if server is not set or there is no connected server
            if (_server.NodeManager.OutgoingNodes.Length == 0)
                return;

            byte[] mdata = HorseProtocolWriter.Create(message);
            foreach (OutgoingNode node in _server.NodeManager.OutgoingNodes)
            {
                if (node?.Client == null)
                    continue;

                bool grant = _server.NodeManager.Authenticator == null || await _server.NodeManager.Authenticator.CanReceive(node.Client, message);

                if (grant)
                    _ = node.Client.SendAsync(mdata);
            }
        }
    }
}