using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;

namespace Horse.Messaging.Server.Channels
{
    internal class ChannelNetworkHandler : INetworkMessageHandler
    {
        private readonly HorseRider _rider;

        public ChannelNetworkHandler(HorseRider rider)
        {
            _rider = rider;
        }

        public Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            try
            {
                return HandleUnsafe(client, message);
            }
            catch
            {
                return client.SendAsync(message.CreateResponse(HorseResultCode.Failed));
            }
        }

        private Task HandleUnsafe(MessagingClient client, HorseMessage message)
        {
            switch (message.ContentType)
            {
                case KnownContentTypes.ChannelPush:

                    /*
                    foreach (IChannelAuthorization authorization in _rider.Channel.Authenticators.All())
                    {
                        if (authorization.CanPush())
                            return client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                    }*/

/*
                    HorseCacheItem item = _cache.Get(message.Target);
                    if (item == null)
                        return client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                    HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                    response.SetSource(message.Target);
                    response.Content = item.Value;
                    return client.SendAsync(response);
*/
                    return Task.CompletedTask;

                case KnownContentTypes.ChannelCreate:
                    return Task.CompletedTask;

                case KnownContentTypes.ChannelRemove:
                    return Task.CompletedTask;

                case KnownContentTypes.ChannelUpdate:
                    return Task.CompletedTask;

                case KnownContentTypes.ChannelList:
                    return Task.CompletedTask;


                default:
                    return Task.CompletedTask;
            }
        }
    }
}