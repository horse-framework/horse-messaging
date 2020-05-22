using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Routing;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class RouterMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        internal RouterMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            bool pendingAck = message.PendingAcknowledge;
            bool pendingResponse = message.PendingResponse;

            IRouter router = _server.FindRouter(message.Target);
            if (router == null)
            {
                await SendNegativeResponse(client, message, pendingAck, pendingResponse);
                return;
            }

            bool done = await router.Push(client, message);
            if (!done)
                await SendNegativeResponse(client, message, pendingAck, pendingResponse);
        }

        /// <summary>
        /// Sends negative ack or failed response if client is pending ack or response
        /// </summary>
        private static Task SendNegativeResponse(MqClient client, TmqMessage message, bool pendingAck, bool pendingResponse)
        {
            if (pendingAck)
            {
                TmqMessage nack = message.CreateAcknowledge(TmqHeaders.NACK_REASON_NO_CONSUMERS);
                return client.SendAsync(nack);
            }

            if (pendingResponse)
            {
                TmqMessage response = message.CreateResponse(TwinoResultCode.NotFound);
                return client.SendAsync(response);
            }

            return Task.CompletedTask;
        }
    }
}