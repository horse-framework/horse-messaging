using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;

namespace Horse.Messaging.Server.Routing
{
    internal class RouterMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseRider _rider;

        internal RouterMessageHandler(HorseRider rider)
        {
            _rider = rider;
        }

        #endregion

        public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            IRouter router = _rider.Router.Find(message.Target);
            if (router == null)
            {
                await SendResponse(RouterPublishResult.Disabled, client, message);

                foreach (IRouterMessageHandler handler in _rider.Router.MessageHandlers.All())
                    _ = handler.OnRouterNotFound(client, message);

                return;
            }

            RouterPublishResult result = await router.Publish(client, message);
            foreach (IRouterMessageHandler handler in _rider.Router.MessageHandlers.All())
            {
                if (result == RouterPublishResult.OkWillNotRespond || result == RouterPublishResult.OkAndWillBeRespond)
                    _ = handler.OnRouted(client, router, message);
                else
                    _ = handler.OnNotRouted(client, router, message);
            }

            await SendResponse(result, client, message);
            if (router is Router r)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    r.PublishEvent.Trigger(client, router.Name, new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.MessageId));
                else
                    r.PublishEvent.Trigger(client, router.Name);
            }
        }

        /// <summary>
        /// Sends negative ack or failed response if client is pending ack or response
        /// </summary>
        private static Task SendResponse(RouterPublishResult result, MessagingClient client, HorseMessage message)
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