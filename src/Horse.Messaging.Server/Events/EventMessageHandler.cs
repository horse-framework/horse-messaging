using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Events
{
    internal class EventMessageHandler : INetworkMessageHandler
    {
        #region Fields

        private readonly HorseRider _rider;

        private readonly HorseEventType[] _queueEvents =
        {
            HorseEventType.QueueMessageAck,
            HorseEventType.QueueMessageNack,
            HorseEventType.QueueMessageUnack,
            HorseEventType.QueueMessageTimeout,
            HorseEventType.QueuePush
        };

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

            HorseQueue queue = null;
            if (_queueEvents.Contains(type))
            {
                queue = _rider.Queue.Find(message.Target);
                if (queue == null)
                {
                    if (_rider.Options.AutoQueueCreation)
                        queue = await _rider.Queue.Create(message.Target);
                }

                if (queue == null)
                {
                    await SendResponse(client, message, false);
                    return;
                }
            }

            switch (type)
            {
                case HorseEventType.CacheGet:
                    if (subscribe)
                        _rider.Cache.GetEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Cache.GetEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.CacheSet:
                    if (subscribe)
                        _rider.Cache.SetEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Cache.SetEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.CacheRemove:
                    if (subscribe)
                        _rider.Cache.RemoveEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Cache.RemoveEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.CachePurge:
                    if (subscribe)
                        _rider.Cache.PurgeEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Cache.PurgeEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.ChannelCreate:
                    if (subscribe)
                        _rider.Channel.CreateEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Channel.CreateEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.ChannelRemove:
                    if (subscribe)
                        _rider.Channel.RemoveEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Channel.RemoveEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.ChannelSubscribe:
                    if (subscribe)
                        _rider.Channel.SubscribeEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Channel.SubscribeEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.ChannelUnsubscribe:
                    if (subscribe)
                        _rider.Channel.UnsubscribeEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Channel.UnsubscribeEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.ChannelPublish:
                    HorseChannel channel = _rider.Channel.Find(message.Target);
                    if (channel == null)
                    {
                        if (_rider.Channel.Options.AutoChannelCreation)
                            channel = await _rider.Channel.Create(message.Target);

                        if (channel == null)
                        {
                            await SendResponse(client, message, false);
                            return;
                        }
                    }

                    if (subscribe)
                        channel.PublishEvent.Subscribers.Add(client, c => c == client);
                    else
                        channel.PublishEvent.Subscribers.Remove(client);

                    break;

                case HorseEventType.ClientConnect:
                    if (subscribe)
                        _rider.Client.ConnectEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Client.ConnectEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.ClientDisconnect:
                    if (subscribe)
                        _rider.Client.DisconnectEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Client.DisconnectEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.DirectMessage:
                    if (subscribe)
                        _rider.Direct.DirectEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Direct.DirectEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.DirectMessageResponse:
                    if (subscribe)
                        _rider.Direct.ResponseEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Direct.ResponseEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.ConnectedToRemoteNode:
                    if (subscribe)
                        _rider.NodeManager.ConnectedToRemoteNodeEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.NodeManager.ConnectedToRemoteNodeEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.DisconnectedFromRemoteNode:
                    if (subscribe)
                        _rider.NodeManager.DisconnectedFromRemoteNodeEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.NodeManager.DisconnectedFromRemoteNodeEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.RemoteNodeConnect:
                    if (subscribe)
                        _rider.NodeManager.RemoteNodeConnectEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.NodeManager.RemoteNodeConnectEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.RemoteNodeDisconnect:
                    if (subscribe)
                        _rider.NodeManager.RemoteNodeDisconnectEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.NodeManager.RemoteNodeDisconnectEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.QueueCreate:
                    if (subscribe)
                        _rider.Queue.CreateEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Queue.CreateEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.QueueRemove:
                    if (subscribe)
                        _rider.Queue.RemoveEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Queue.RemoveEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.QueueSubscription:
                    if (subscribe)
                        _rider.Queue.SubscriptionEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Queue.SubscriptionEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.QueueUnsubscription:
                    if (subscribe)
                        _rider.Queue.UnsubscriptionEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Queue.UnsubscriptionEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.QueueStatusChange:
                    if (subscribe)
                        _rider.Queue.StatusChangeEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Queue.StatusChangeEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.QueueMessageAck:
                    if (subscribe)
                        queue.MessageAckEvent.Subscribers.Add(client, c => c == client);
                    else
                        queue.MessageAckEvent.Subscribers.Remove(client);
                    break;
                
                case HorseEventType.QueueMessageNack:
                    if (subscribe)
                        queue.MessageNackEvent.Subscribers.Add(client, c => c == client);
                    else
                        queue.MessageNackEvent.Subscribers.Remove(client);
                    break;
                
                case HorseEventType.QueueMessageUnack:
                    if (subscribe)
                        queue.MessageUnackEvent.Subscribers.Add(client, c => c == client);
                    else
                        queue.MessageUnackEvent.Subscribers.Remove(client);
                    break;
                
                case HorseEventType.QueueMessageTimeout:
                    if (subscribe)
                        queue.MessageTimeoutEvent.Subscribers.Add(client, c => c == client);
                    else
                        queue.MessageTimeoutEvent.Subscribers.Remove(client);
                    break;
                
                case HorseEventType.QueuePush:
                    if (subscribe)
                        queue.PushEvent.Subscribers.Add(client, c => c == client);
                    else
                        queue.PushEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.RouterCreate:
                    if (subscribe)
                        _rider.Router.CreateEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Router.CreateEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.RouterRemove:
                    if (subscribe)
                        _rider.Router.RemoveEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Router.RemoveEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.RouterBindingAdd:
                    if (subscribe)
                        _rider.Router.BindingAddEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Router.BindingAddEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.RouterBindingRemove:
                    if (subscribe)
                        _rider.Router.BindingRemoveEvent.Subscribers.Add(client, c => c == client);
                    else
                        _rider.Router.BindingRemoveEvent.Subscribers.Remove(client);
                    break;

                case HorseEventType.RouterPublish:
                    Router router = _rider.Router.Find(message.Target) as Router;
                    if (router == null)
                    {
                        await SendResponse(client, message, false);
                        return;
                    }

                    if (subscribe)
                        router.PublishEvent.Subscribers.Add(client, c => c == client);
                    else
                        router.PublishEvent.Subscribers.Remove(client);

                    break;

                default:
                    return;
            }

            await SendResponse(client, message, true);
        }
    }
}