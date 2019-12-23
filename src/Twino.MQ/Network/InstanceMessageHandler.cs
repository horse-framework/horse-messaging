using System.Threading.Tasks;
using Twino.Client.TMQ.Connectors;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class InstanceMessageHandler : INetworkMessageHandler
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

        public InstanceMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            //to prevent receive same message from instanced server that already sent from this server
            if (client.IsInstanceServer)
                return;

            //if server is not set or there is no connected server
            if (_server.ServerAuthenticator == null || _server.InstanceConnectors.Length == 0)
                return;

            byte[] mdata = await _writer.Create(message);
            foreach (TmqStickyConnector connector in _server.InstanceConnectors)
            {
                bool grant = await _server.ServerAuthenticator.CanReceive(connector.GetClient(), message);

                if (grant)
                    connector.Send(mdata);
            }
        }
    }
}