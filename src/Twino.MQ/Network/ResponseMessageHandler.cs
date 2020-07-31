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
        private readonly TwinoMQ _server;

        public ResponseMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient sender, TmqMessage message, bool fromNode)
        {
            //server does not care response messages
            //if receiver could be found, message is sent to it's receiver
            //if receiver isn't available, response will be thrown

            MqClient receiver = _server.FindClient(message.Target);

            if (receiver != null)
            {
                //check sending message authority
                if (_server.Authorization != null)
                {
                    bool grant = await _server.Authorization.CanResponseMessage(sender, message, receiver);
                    if (!grant)
                        return;
                }

                await receiver.SendAsync(message);
            }
        }
    }
}