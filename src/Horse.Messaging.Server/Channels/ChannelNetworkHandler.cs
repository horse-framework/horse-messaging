using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Channels
{
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

                    if (channel != null)
                    {
                        channel.RemoveClient(client);
                    }

                    if (waitResponse)
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Ok));
                    break;
                }

                case KnownContentTypes.ChannelList:
                {
                    break;
                }
            }
        }
    }
}