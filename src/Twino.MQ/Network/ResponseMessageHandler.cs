using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class ResponseMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public ResponseMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient sender, TmqMessage message)
        {
            //server does not care response messages
            //if receiver could be found, message is sent to it's receiver
            //if receiver isn't available, response will be thrown

            MqClient receiver = _server.FindClient(message.Target);

            if (receiver != null)
                await receiver.SendAsync(message);
        }
    }
}