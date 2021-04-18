using System;
using System.Data;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;

namespace Horse.Messaging.Server.Cache
{
    internal class CacheNetworkHandler : INetworkMessageHandler
    {
        private readonly HorseMq _server;
        private readonly HorseCache _cache;

        public CacheNetworkHandler(HorseMq server, HorseCache cache)
        {
            _server = server;
            _cache = cache;
        }

        public Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            try
            {
                return HandleUnsafe(client, message);
            }
            catch (OperationCanceledException)
            {
                return client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));
            }
            catch (DuplicateNameException)
            {
                return client.SendAsync(message.CreateResponse(HorseResultCode.Duplicate));
            }
        }

        private Task HandleUnsafe(MessagingClient client, HorseMessage message)
        {
            switch (message.ContentType)
            {
                //get cache item
                case KnownContentTypes.GetCache:

                    foreach (ICacheAuthorization authorization in _cache.GetAuthorizations())
                    {
                        if (!authorization.CanGet(client, message.Target))
                            return client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                    }

                    HorseCacheItem item = _cache.Get(message.Target);
                    if (item == null)
                        return client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                    HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                    response.SetSource(message.Target);
                    response.Content = item.Value;
                    return client.SendAsync(response);

                //set cache item
                case KnownContentTypes.SetCache:

                    foreach (ICacheAuthorization authorization in _cache.GetAuthorizations())
                    {
                        if (!authorization.CanSet(client, message.Target, message.Content))
                            return client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                    }

                    string messageTimeout = message.FindHeader(HorseHeaders.MESSAGE_TIMEOUT);

                    TimeSpan timeout = TimeSpan.Zero;
                    if (!string.IsNullOrEmpty(messageTimeout))
                        timeout = TimeSpan.FromSeconds(Convert.ToInt32(messageTimeout));

                    CacheOperation operation = _cache.Set(message.Target, message.Content, timeout);
                    switch (operation.Result)
                    {
                        case CacheResult.Ok:
                            return client.SendAsync(message.CreateResponse(HorseResultCode.Ok));

                        case CacheResult.KeyLimit:
                            return client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));

                        case CacheResult.KeySizeLimit:
                            return client.SendAsync(message.CreateResponse(HorseResultCode.NameSizeLimit));

                        case CacheResult.ItemSizeLimit:
                            return client.SendAsync(message.CreateResponse(HorseResultCode.ValueSizeLimit));

                        default:
                            return client.SendAsync(message.CreateResponse(HorseResultCode.Failed));
                    }

                //remove cache item
                case KnownContentTypes.RemoveCache:

                    foreach (ICacheAuthorization authorization in _cache.GetAuthorizations())
                    {
                        if (!authorization.CanRemove(client, message.Target))
                            return client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                    }

                    _cache.Remove(message.Target);
                    return client.SendAsync(message.CreateResponse(HorseResultCode.Ok));

                //purge all caches
                case KnownContentTypes.PurgeCache:

                    foreach (ICacheAuthorization authorization in _cache.GetAuthorizations())
                    {
                        if (!authorization.CanPurge(client))
                            return client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                    }

                    _cache.Purge();
                    return client.SendAsync(message.CreateResponse(HorseResultCode.Ok));

                default:
                    return Task.CompletedTask;
            }
        }
    }
}