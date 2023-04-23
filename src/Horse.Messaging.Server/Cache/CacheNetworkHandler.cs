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

                    HorseCacheItem item = _cache.Get(message.Target, out bool firstWarningReceiver);
                    if (item == null)
                    {
                        await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
                        return;
                    }

                    HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                    response.SetSource(message.Target);

                    response.AddHeader(HorseHeaders.EXPIRY, item.Expiration.ToUnixMilliseconds().ToString());

                    if (item.ExpirationWarning.HasValue)
                    {
                        response.AddHeader(HorseHeaders.WARNING, item.ExpirationWarning.Value.ToUnixMilliseconds().ToString());
                        if (item.ExpirationWarnCount > 0)
                            response.AddHeader(HorseHeaders.WARN_COUNT, item.ExpirationWarnCount.ToString());
                    }

                    response.HighPriority = firstWarningReceiver;
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
                    string warningDuration = message.FindHeader(HorseHeaders.WARNING_DURATION);
                    string tags = message.FindHeader(HorseHeaders.TAG);

                    string[] tagNames = string.IsNullOrEmpty(tags) ? Array.Empty<string>() : tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

                    TimeSpan timeout = TimeSpan.Zero;
                    TimeSpan? warning = null;

                    if (!string.IsNullOrEmpty(messageTimeout))
                        timeout = TimeSpan.FromSeconds(Convert.ToInt32(messageTimeout));

                    if (!string.IsNullOrEmpty(warningDuration))
                        warning = TimeSpan.FromSeconds(Convert.ToInt32(warningDuration));

                    CacheOperation operation = _cache.Set(message.Target, message.Content, timeout, warning, tagNames);
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

                    string tagName = message.FindHeader(HorseHeaders.TAG);

                    if (string.IsNullOrEmpty(tagName))
                        _cache.Purge();
                    else
                        _cache.PurgeByTag(tagName.Trim());

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