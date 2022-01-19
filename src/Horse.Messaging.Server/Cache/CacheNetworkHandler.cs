using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Network;

namespace Horse.Messaging.Server.Cache
{
    internal class CacheNetworkHandler : INetworkMessageHandler
    {
        private readonly HorseRider _rider;
        private readonly HorseCache _cache;

        public CacheNetworkHandler(HorseRider rider)
        {
            _rider = rider;
            _cache = rider.Cache;
        }

        public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            try
            {
                await HandleUnsafe(client, message);
            }
            catch (OperationCanceledException)
            {
                await client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));
            }
            catch
            {
                await client.SendAsync(message.CreateResponse(HorseResultCode.Failed));
            }
        }

        private async Task HandleUnsafe(MessagingClient client, HorseMessage message)
        {
            switch (message.ContentType)
            {
                //get cache item
                case KnownContentTypes.GetCache:
                {
                    foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                    {
                        if (!authorization.CanGet(client, message.Target))
                        {
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                            return;
                        }
                    }

                    HorseCacheItem item = _cache.Get(message.Target);
                    if (item == null)
                    {
                        await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
                        return;
                    }

                    HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                    response.SetSource(message.Target);
                    response.Content = item.Value;
                    await client.SendAsync(response);
                    _cache.GetEvent.Trigger(client, message.Target);
                    return;
                }
                
                //set cache item
                case KnownContentTypes.SetCache:
                {
                    foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                    {
                        if (!authorization.CanSet(client, message.Target, message.Content))
                        {
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                            return;
                        }
                    }

                    string messageTimeout = message.FindHeader(HorseHeaders.MESSAGE_TIMEOUT);

                    TimeSpan timeout = TimeSpan.Zero;
                    if (!string.IsNullOrEmpty(messageTimeout))
                        timeout = TimeSpan.FromSeconds(Convert.ToInt32(messageTimeout));

                    CacheOperation operation = _cache.Set(message.Target, message.Content, timeout);
                    switch (operation.Result)
                    {
                        case CacheResult.Ok:
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Ok));
                            _cache.SetEvent.Trigger(client, message.Target);
                            return;

                        case CacheResult.KeyLimit:
                            await client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));
                            return;

                        case CacheResult.KeySizeLimit:
                            await client.SendAsync(message.CreateResponse(HorseResultCode.NameSizeLimit));
                            return;

                        case CacheResult.ItemSizeLimit:
                            await client.SendAsync(message.CreateResponse(HorseResultCode.ValueSizeLimit));
                            return;

                        default:
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Failed));
                            return;
                    }
                }
                    
                //remove cache item
                case KnownContentTypes.RemoveCache:
                {
                    foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                    {
                        if (!authorization.CanRemove(client, message.Target))
                        {
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                            return;
                        }
                    }

                    _cache.Remove(message.Target);
                    await client.SendAsync(message.CreateResponse(HorseResultCode.Ok));
                    _cache.RemoveEvent.Trigger(client, message.Target);
                    return;
                }
                
                //purge all caches
                case KnownContentTypes.PurgeCache:
                {
                    foreach (ICacheAuthorization authorization in _cache.Authorizations.All())
                    {
                        if (!authorization.CanPurge(client))
                        {
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                            return;
                        }
                    }

                    _cache.Purge();
                    await client.SendAsync(message.CreateResponse(HorseResultCode.Ok));
                    _cache.PurgeEvent.Trigger(client);
                    return;
                }

                case KnownContentTypes.GetCacheList:
                {
                    string filter = message.FindHeader(HorseHeaders.FILTER);
                    List<CacheInformation> caches = _cache.GetCacheKeys();

                    if (!string.IsNullOrEmpty(filter))
                        caches = caches.Where(x => Filter.CheckMatch(x.Key, filter)).ToList();
                    
                    HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                    response.ContentType = KnownContentTypes.ChannelList;
                    response.Serialize(caches, _rider.MessageContentSerializer);
                    await client.SendAsync(response);
                    return;
                }
            }
        }
    }
}