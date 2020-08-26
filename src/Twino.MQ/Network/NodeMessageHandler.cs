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
        private readonly TwinoMQ _server;

        /// <summary>
        /// Default TMQ protocol message writer
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

        public NodeMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TwinoMessage message, bool fromNode)
        {
            //if server is not set or there is no connected server
            if (_server.NodeManager.Connectors.Length == 0)
                return;

            byte[] mdata = TmqWriter.Create(message);
            foreach (TmqStickyConnector connector in _server.NodeManager.Connectors)
            {
                bool grant = _server.NodeManager.Authenticator == null || await _server.NodeManager.Authenticator.CanReceive(connector.GetClient(), message);

                if (grant)
                    _ = connector.SendAsync(mdata);
            }
        }
    }
}