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
        private readonly TwinoMQ _server;

        internal RouterMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TwinoMessage message, bool fromNode)
        {
            bool pendingAck = message.PendingAcknowledge;
            bool pendingResponse = message.PendingResponse;

            IRouter router = _server.FindRouter(message.Target);
            if (router == null)
            {
                await SendResponse(RouterPublishResult.Disabled, client, message, pendingAck, pendingResponse);
                return;
            }

            RouterPublishResult result = await router.Publish(client, message);
            await SendResponse(result, client, message, pendingAck, pendingResponse);
        }

        /// <summary>
        /// Sends negative ack or failed response if client is pending ack or response
        /// </summary>
        private static Task SendResponse(RouterPublishResult result, MqClient client, TwinoMessage message, bool pendingAck, bool pendingResponse)
        {
            if (result == RouterPublishResult.OkAndWillBeRespond)
                return Task.CompletedTask;

            bool positive = result == RouterPublishResult.OkWillNotRespond;
            
            if (pendingAck)
            {
                TwinoMessage ack = positive ? message.CreateAcknowledge() : message.CreateAcknowledge(TmqHeaders.NACK_REASON_NO_CONSUMERS);
                return client.SendAsync(ack);
            }

            if (pendingResponse)
            {
                TwinoMessage response = positive ? message.CreateResponse(TwinoResultCode.Ok) : message.CreateResponse(TwinoResultCode.NotFound);
                return client.SendAsync(response);
            }

            return Task.CompletedTask;
        }
    }
}