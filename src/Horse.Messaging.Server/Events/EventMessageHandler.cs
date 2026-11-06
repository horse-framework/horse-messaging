using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Events;

internal class EventMessageHandler : INetworkMessageHandler
{
    #region Fields

    private readonly HorseRider _rider;

    public EventMessageHandler(HorseRider rider)
    {
        _rider = rider;
    }

    #endregion

    private static async Task SendResponse(MessagingClient client, HorseMessage message, bool successful)
    {
        ushort contentType = successful ? (ushort) HorseResultCode.Ok : (ushort) HorseResultCode.Failed;
        HorseMessage response = new HorseMessage(MessageType.Response, client.UniqueId, contentType);
        response.SetMessageId(message.MessageId);
        await client.SendAsync(response);
    }

    public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
    {
        HorseEventType type = (HorseEventType) message.ContentType;
        bool subscribe = true;
        string s = message.FindHeader(HorseHeaders.SUBSCRIBE);
        if (!string.IsNullOrEmpty(s) && s.Equals("NO", StringComparison.CurrentCultureIgnoreCase))
            subscribe = false;

        if (subscribe)
        {
            foreach (IClientAuthorization authorization in _rider.Client.Authorizations.All())
            {
                if (!authorization.CanSubscribeEvent(client, type, message.Target))
                {
                    await SendResponse(client, message, false);
                    return;
                }
            }
        }

        // Queue and router events are kept by target name in the registry,
        // the subscription stays valid when the target is removed and created again.
        // Channel publish subscriptions belong to the channel and end when it is removed.
        if (IsTargetEvent(type))
        {
            if (string.IsNullOrEmpty(message.Target))
            {
                await SendResponse(client, message, false);
                return;
            }

            // Unsubscribing needs no target, it may already be removed.
            if (!subscribe)
            {
                if (type == HorseEventType.ChannelPublish)
                    _rider.Channel.Find(message.Target)?.PublishEvent.Unsubscribe(client);
                else
                    _rider.EventSubscriptions.Unsubscribe(type, message.Target, client);

                await SendResponse(client, message, true);
                return;
            }

            // Only a subscription may create the target.
            // A channel can be removed while the client subscribes, its subscriptions end with it.
            // Then the current channel with that name is found (or created) once more.
            for (int attempt = 0; attempt < 2 && client.IsConnected; attempt++)
            {
                EventManager targetEvent = await FindOrCreateTargetEvent(client, message, type);
                if (targetEvent == null)
                    break;

                if (targetEvent.Subscribe(client))
                {
                    await SendResponse(client, message, true);
                    return;
                }
            }

            await SendResponse(client, message, false);
            return;
        }

        EventManager manager = FindRiderEvent(type);
        if (manager == null)
        {
            // unknown event type, the client waits for a response
            await SendResponse(client, message, false);
            return;
        }

        bool successful = true;
        if (subscribe)
            successful = manager.Subscribe(client);
        else
            manager.Unsubscribe(client);

        await SendResponse(client, message, successful);
    }

    private static bool IsTargetEvent(HorseEventType type)
    {
        return type is HorseEventType.QueueMessageAck
            or HorseEventType.QueueMessageNack
            or HorseEventType.QueueMessageUnack
            or HorseEventType.QueueMessageTimeout
            or HorseEventType.QueuePush
            or HorseEventType.ChannelPublish
            or HorseEventType.RouterPublish;
    }

    private async Task<EventManager> FindOrCreateTargetEvent(MessagingClient client, HorseMessage message, HorseEventType type)
    {
        switch (type)
        {
            case HorseEventType.ChannelPublish:
            {
                HorseChannel channel = _rider.Channel.Find(message.Target);
                if (channel == null && _rider.Channel.Options.AutoChannelCreation)
                {
                    // Same path as auto creation on push and subscribe: a concurrent create returns the existing channel,
                    // an invalid name or the channel limit returns null (the client gets Failed) instead of throwing,
                    // which made the network handler disconnect the client.
                    HorseChannelOptions options = HorseChannelOptions.Clone(_rider.Channel.Options);
                    channel = await _rider.Channel.Create(message.Target, options, null, true, true, true);
                }

                return channel?.PublishEvent;
            }

            case HorseEventType.RouterPublish:
                return (_rider.Router.Find(message.Target) as Router)?.PublishEvent;

            default:
            {
                HorseQueue queue = _rider.Queue.Find(message.Target);
                if (queue == null && _rider.Queue.Options.AutoQueueCreation)
                {
                    // Same path as auto creation on push: no exception for a concurrent create of the same name.
                    QueueOptions options = QueueOptions.CloneFrom(_rider.Queue.Options);
                    queue = await _rider.Queue.Create(message.Target, options, message, true, true, client);
                }

                if (queue == null)
                    return null;

                return type switch
                {
                    HorseEventType.QueueMessageAck => queue.MessageAckEvent,
                    HorseEventType.QueueMessageNack => queue.MessageNackEvent,
                    HorseEventType.QueueMessageUnack => queue.MessageUnackEvent,
                    HorseEventType.QueueMessageTimeout => queue.MessageTimeoutEvent,
                    HorseEventType.QueuePush => queue.PushEvent,
                    _ => null
                };
            }
        }
    }

    private EventManager FindRiderEvent(HorseEventType type)
    {
        return type switch
        {
            HorseEventType.CacheGet => _rider.Cache.GetEvent,
            HorseEventType.CacheSet => _rider.Cache.SetEvent,
            HorseEventType.CacheRemove => _rider.Cache.RemoveEvent,
            HorseEventType.CachePurge => _rider.Cache.PurgeEvent,
            HorseEventType.ChannelCreate => _rider.Channel.CreateEvent,
            HorseEventType.ChannelRemove => _rider.Channel.RemoveEvent,
            HorseEventType.ChannelSubscribe => _rider.Channel.SubscribeEvent,
            HorseEventType.ChannelUnsubscribe => _rider.Channel.UnsubscribeEvent,
            HorseEventType.ClientConnect => _rider.Client.ConnectEvent,
            HorseEventType.ClientDisconnect => _rider.Client.DisconnectEvent,
            HorseEventType.DirectMessage => _rider.Direct.DirectEvent,
            HorseEventType.DirectMessageResponse => _rider.Direct.ResponseEvent,
            HorseEventType.ConnectedToRemoteNode => _rider.Cluster.ConnectedToRemoteNodeEvent,
            HorseEventType.DisconnectedFromRemoteNode => _rider.Cluster.DisconnectedFromRemoteNodeEvent,
            HorseEventType.RemoteNodeConnect => _rider.Cluster.RemoteNodeConnectEvent,
            HorseEventType.RemoteNodeDisconnect => _rider.Cluster.RemoteNodeDisconnectEvent,
            HorseEventType.QueueCreate => _rider.Queue.CreateEvent,
            HorseEventType.QueueRemove => _rider.Queue.RemoveEvent,
            HorseEventType.QueueSubscription => _rider.Queue.SubscriptionEvent,
            HorseEventType.QueueUnsubscription => _rider.Queue.UnsubscriptionEvent,
            HorseEventType.QueueStatusChange => _rider.Queue.StatusChangeEvent,
            HorseEventType.QueuePartitionCreated => _rider.Queue.PartitionCreatedEvent,
            HorseEventType.RouterCreate => _rider.Router.CreateEvent,
            HorseEventType.RouterRemove => _rider.Router.RemoveEvent,
            HorseEventType.RouterBindingAdd => _rider.Router.BindingAddEvent,
            HorseEventType.RouterBindingRemove => _rider.Router.BindingRemoveEvent,
            _ => null
        };
    }
}
