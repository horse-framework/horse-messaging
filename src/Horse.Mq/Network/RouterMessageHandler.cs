using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Routing;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Network
{
    internal class RouterMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseMq _server;

        internal RouterMessageHandler(HorseMq server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, HorseMessage message, bool fromNode)
        {
            IRouter router = _server.FindRouter(message.Target);
            if (router == null)
            {
                await SendResponse(RouterPublishResult.Disabled, client, message);

                foreach (IRouterMessageHandler handler in _server.RouterMessageHandlers)
                    _ = handler.OnRouterNotFound(client, message);

                return;
            }

            RouterPublishResult result = await router.Publish(client, message);
            foreach (IRouterMessageHandler handler in _server.RouterMessageHandlers)
            {
                if (result == RouterPublishResult.OkWillNotRespond || result == RouterPublishResult.OkAndWillBeRespond)
                    _ = handler.OnRouted(client, router, message);
                else
                    _ = handler.OnNotRouted(client, router, message);
            }

            await SendResponse(result, client, message);
        }

        /// <summary>
        /// Sends negative ack or failed response if client is pending ack or response
        /// </summary>
        private static Task SendResponse(RouterPublishResult result, MqClient client, HorseMessage message)
        {
            if (result == RouterPublishResult.OkAndWillBeRespond)
                return Task.CompletedTask;

            bool positive = result == RouterPublishResult.OkWillNotRespond;
            if (message.WaitResponse)
            {
                HorseMessage response = positive
                                            ? message.CreateAcknowledge()
                                            : message.CreateResponse(HorseResultCode.NotFound);

                return client.SendAsync(response);
            }

            return Task.CompletedTask;
        }
    }
}