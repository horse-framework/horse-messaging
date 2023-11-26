using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Channels;

internal class ChannelNetworkHandler : INetworkMessageHandler
{
    private readonly HorseRider _rider;

    public ChannelNetworkHandler(HorseRider rider)
    {
        _rider = rider;
    }

    public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
    {
        try
        {
            await HandleUnsafe(client, message);
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
            case KnownContentTypes.ChannelPush:
            {
                bool waitResponse = message.WaitResponse;
                foreach (IChannelAuthorization authorization in _rider.Channel.Authenticators.All())
                {
                    if (!authorization.CanPush(client, message))
                    {
                        if (waitResponse)
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));

                        return;
                    }
                }

                HorseChannel channel = _rider.Channel.Find(message.Target);

                if (channel == null)
                {
                    if (_rider.Channel.Options.AutoChannelCreation)
                        channel = await _rider.Channel.Create(message.Target);

                    if (channel == null)
                    {
                        if (waitResponse)
                            await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                        return;
                    }
                }

                PushResult result = channel.Push(message);

                if (result == PushResult.Success)
                    client.Stats.ChannelPublishes++;

                if (waitResponse)
                {
                    HorseMessage response = result == PushResult.Success
                        ? message.CreateResponse(HorseResultCode.Ok)
                        : message.CreateAcknowledge(result.ToString());

                    response.SetSource(message.Target);
                    await client.SendAsync(response);
                }

                break;
            }

            case KnownContentTypes.ChannelCreate:
            {
                foreach (IChannelAuthorization authorization in _rider.Channel.Authenticators.All())
                {
                    if (!authorization.CanPush(client, message))
                    {
                        if (message.WaitResponse)
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));

                        return;
                    }
                }

                HorseChannel channel = _rider.Channel.Find(message.Target);
                HorseMessage response;
                if (channel != null)
                    response = message.CreateResponse(HorseResultCode.Ok);
                else
                {
                    channel = await _rider.Channel.Create(message.Target);
                    response = channel != null
                        ? message.CreateResponse(HorseResultCode.Ok)
                        : message.CreateAcknowledge("Failed");
                }

                response.SetSource(message.Target);
                await client.SendAsync(response);
                break;
            }

            case KnownContentTypes.ChannelRemove:
            {
                foreach (IChannelAuthorization authorization in _rider.Channel.Authenticators.All())
                {
                    if (!authorization.CanPush(client, message))
                    {
                        if (message.WaitResponse)
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));

                        return;
                    }
                }

                HorseChannel channel = _rider.Channel.Find(message.Target);
                if (channel != null)
                    _rider.Channel.Remove(channel);

                HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                response.SetSource(message.Target);
                await client.SendAsync(response);
                break;
            }

            case KnownContentTypes.ChannelSubscribe:
            {
                bool waitResponse = message.WaitResponse;
                HorseChannel channel = _rider.Channel.Find(message.Target);

                if (channel == null)
                {
                    if (_rider.Channel.Options.AutoChannelCreation)
                        channel = await _rider.Channel.Create(message.Target);

                    if (channel == null)
                    {
                        if (waitResponse)
                            await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                        return;
                    }
                }

                SubscriptionResult result = channel.AddClient(client);
                if (waitResponse)
                {
                    HorseMessage response;
                    switch (result)
                    {
                        case SubscriptionResult.Success:
                            response = message.CreateResponse(HorseResultCode.Ok);
                            break;

                        case SubscriptionResult.Unauthorized:
                            response = message.CreateResponse(HorseResultCode.Unauthorized);
                            break;

                        default:
                            response = message.CreateResponse(HorseResultCode.Failed);
                            break;
                    }

                    response.SetSource(message.Target);
                    await client.SendAsync(response);
                }

                break;
            }

            case KnownContentTypes.ChannelUnsubscribe:
            {
                bool waitResponse = message.WaitResponse;
                HorseChannel channel = _rider.Channel.Find(message.Target);
                channel?.RemoveClient(client);

                if (waitResponse)
                    await client.SendAsync(message.CreateResponse(HorseResultCode.Ok));
                break;
            }

            case KnownContentTypes.ChannelList:
            {
                string filter = message.FindHeader(HorseHeaders.FILTER);
                List<ChannelInformation> list = new List<ChannelInformation>();
                foreach (HorseChannel channel in _rider.Channel.Channels)
                {
                    if (channel == null)
                        continue;

                    if (!string.IsNullOrEmpty(filter) && !Filter.CheckMatch(channel.Name, filter))
                        continue;

                    list.Add(channel.Info);
                }

                HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                response.ContentType = KnownContentTypes.ChannelList;
                response.Serialize(list, _rider.MessageContentSerializer);
                await client.SendAsync(response);
                break;
            }

            case KnownContentTypes.ChannelSubscribers:
            {
                HorseChannel channel = _rider.Channel.Find(message.Target);

                if (channel == null)
                {
                    await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
                    return;
                }

                foreach (IAdminAuthorization authorization in _rider.Client.AdminAuthorizations.All())
                {
                    bool grant = await authorization.CanReceiveChannelSubscribers(client, channel);
                    if (!grant)
                    {
                        if (message.WaitResponse)
                            await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));

                        return;
                    }
                }

                List<ClientInformation> list = new List<ClientInformation>();

                foreach (ChannelClient cc in channel.Clients)
                    list.Add(new ClientInformation
                    {
                        Id = cc.Client.UniqueId,
                        Name = cc.Client.Name,
                        Type = cc.Client.Type,
                        IsAuthenticated = cc.Client.IsAuthenticated,
                        Online = cc.SubscribedAt.LifetimeMilliseconds(),
                    });

                HorseMessage response = message.CreateResponse(HorseResultCode.Ok);
                message.ContentType = KnownContentTypes.ChannelSubscribers;
                response.Serialize(list, _rider.MessageContentSerializer);
                await client.SendAsync(response);

                break;
            }
        }
    }
}