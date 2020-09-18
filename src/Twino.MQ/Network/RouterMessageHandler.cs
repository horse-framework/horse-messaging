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
            IRouter router = _server.FindRouter(message.Target);
            if (router == null)
            {
                await SendResponse(RouterPublishResult.Disabled, client, message);
                return;
            }

            RouterPublishResult result = await router.Publish(client, message);
            await SendResponse(result, client, message);
        }

        /// <summary>
        /// Sends negative ack or failed response if client is pending ack or response
        /// </summary>
        private static Task SendResponse(RouterPublishResult result, MqClient client, TwinoMessage message)
        {
            if (result == RouterPublishResult.OkAndWillBeRespond)
                return Task.CompletedTask;

            bool positive = result == RouterPublishResult.OkWillNotRespond;
            if (message.WaitResponse)
            {
                TwinoMessage response = positive
                                            ? message.CreateAcknowledge()
                                            : message.CreateResponse(TwinoResultCode.NotFound);

                return client.SendAsync(response);
            }

            return Task.CompletedTask;
        }
    }
}