using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Plugins;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Channels;

/// <summary>
/// Horse Channel
/// </summary>
public class HorseChannel
{
    #region Properties

    /// <summary>
    /// Unique name (not case-sensetive)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Queue topic
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Root horse rider
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Channel status
    /// </summary>
    public ChannelStatus Status { get; set; }

    /// <summary>
    /// Channel options.
    /// If null, default channel options will be used
    /// </summary>
    public HorseChannelOptions Options { get; }

    /// <summary>
    /// Channel Information
    /// </summary>
    public ChannelInformation Info { get; }

    /// <summary>
    /// Payload object for end-user usage
    /// </summary>
    public object Payload { get; set; }

    /// <summary>
    /// The UTC date last message is published
    /// </summary>
    public DateTime LastPublishDate { get; private set; }

    /// <summary>
    /// Event Manager for HorseEventType.ChannelPublish
    /// </summary>
    public EventManager PublishEvent { get; }

    /// <summary>
    /// Returns all clients in the channel.
    /// </summary>
    public IEnumerable<ChannelClient> Clients => _channelClients.Where(x => x != null);

    private Timer _destoryTimer;
    private byte[] _initialMessage;
    private readonly object _channelClientLock = new();
    private readonly ChannelClient[] _channelClients = new ChannelClient[256];

    #endregion

    #region Constructors - Destroy

    internal HorseChannel(HorseRider rider, string name, HorseChannelOptions options)
    {
        Rider = rider;
        Name = name;
        Options = options;
        Status = ChannelStatus.Running;
        PublishEvent = new EventManager(rider, HorseEventType.ChannelPublish, name);
        Info = new ChannelInformation
        {
            Name = name,
            Status = Status.ToString()
        };

        if (options.ClientLimit > 1)
            _channelClients = new ChannelClient[options.ClientLimit];

        _destoryTimer = new Timer(s =>
        {
            if (Options.AutoDestroy && DateTime.UtcNow - LastPublishDate > TimeSpan.FromSeconds(options.AutoDestroyIdleSeconds) && ClientsCount() == 0)
                Rider.Channel.Remove(this);
        }, null, 15000, 15000);
    }

    internal void Destroy()
    {
        if (_destoryTimer != null)
        {
            _destoryTimer.Dispose();
            _destoryTimer = null;
        }
    }

    internal void UpdateOptionsByMessage(HorseMessage message)
    {
        string clientLimit = message.FindHeader(HorseHeaders.CLIENT_LIMIT);
        if (!string.IsNullOrEmpty(clientLimit))
            Options.ClientLimit = Convert.ToInt32(clientLimit.Trim());

        string messageSizeLimit = message.FindHeader(HorseHeaders.MESSAGE_SIZE_LIMIT);
        if (!string.IsNullOrEmpty(messageSizeLimit))
            Options.MessageSizeLimit = Convert.ToUInt64(messageSizeLimit.Trim());

        string autoDestroy = message.FindHeader(HorseHeaders.AUTO_DESTROY);
        if (!string.IsNullOrEmpty(autoDestroy))
        {
            string value = autoDestroy.Trim();
            Options.AutoDestroy = value.Equals("TRUE", StringComparison.CurrentCultureIgnoreCase) || value == "1";
        }

        string initialMessage = message.FindHeader(HorseHeaders.CHANNEL_INITIAL_MESSAGE);
        if (!string.IsNullOrEmpty(initialMessage))
        {
            string value = initialMessage.Trim();
            Options.SendLastMessageAsInitial = value.Equals("TRUE", StringComparison.CurrentCultureIgnoreCase) || value == "1";
        }

        string idleSeconds = message.FindHeader(HorseHeaders.CHANNEL_DESTROY_IDLE_SECONDS);
        if (!string.IsNullOrEmpty(idleSeconds))
            Options.AutoDestroyIdleSeconds = Convert.ToInt32(idleSeconds.Trim());

        Rider.Channel.ClusterNotifier.SendChannelUpdated(this);
    }

    #endregion

    #region Delivery

    /// <summary>
    /// Pushes new message into the queue
    /// </summary>
    public PushResult Push(string message)
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, Name);
        msg.SetStringContent(message);
        return Push(msg);
    }

    /// <summary>
    /// Pushes a message into the queue.
    /// </summary>
    internal PushResult Push(HorseMessage message)
    {
        if (Status == ChannelStatus.Paused)
            return PushResult.StatusNotSupported;

        if (Options.MessageSizeLimit > 0 && message.Length > Options.MessageSizeLimit)
            return PushResult.LimitExceeded;

        //remove operational headers that are should not be sent to consumers or saved to disk
        message.RemoveHeaders(HorseHeaders.CHANNEL_NAME, HorseHeaders.CC);
        message.WaitResponse = false;
        LastPublishDate = DateTime.UtcNow;

        try
        {
            byte[] messageData = HorseProtocolWriter.Create(message);
            _initialMessage = messageData;

            int count = 0;
            //to all receivers
            foreach (ChannelClient client in _channelClients)
            {
                if (client == null)
                    break;

                //to only online receivers
                if (!client.Client.IsConnected)
                    continue;

                //send the message
                _ = client.Client.SendRawAsync(messageData);
                count++;
            }

            Interlocked.Increment(ref Info.PublishedValue);
            Interlocked.Add(ref Info.ReceivedValue, count);

            PublishEvent.Trigger(Name);

            foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
                _ = handler.OnPublish(this, message);

            Rider.Plugin.TriggerPluginHandlers(HorsePluginEvent.ChannelPublish, Name, message);
            
            return PushResult.Success;
        }
        catch (Exception ex)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ChannelPush, "Channel Push Error: " + Name, ex);
            return PushResult.Error;
        }
    }

    /// <summary>
    /// Returns latest published message
    /// </summary>
    public async Task<HorseMessage> GetInitialMessage()
    {
        if (_initialMessage == null)
            return null;

        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage message = await reader.Read(new MemoryStream(_initialMessage));
        return message;
    }

    #endregion

    #region Client Actions

    /// <summary>
    /// Returns client count in the queue
    /// </summary>
    /// <returns></returns>
    public int ClientsCount()
    {
        for (int i = 0; i < _channelClients.Length; i++)
        {
            if (_channelClients[i] == null)
                return i;
        }

        return _channelClients.Length;
    }

    /// <summary>
    /// Adds the client to the queue
    /// </summary>
    public SubscriptionResult AddClient(MessagingClient client)
    {
        foreach (IChannelAuthorization authenticator in Rider.Channel.Authenticators.All())
        {
            bool allowed = authenticator.CanSubscribe(this, client);
            if (!allowed)
                return SubscriptionResult.Unauthorized;
        }

        ChannelClient channelClient = new ChannelClient(this, client);
        bool added = false;
        lock (_channelClientLock)
        {
            for (int i = 0; i < _channelClients.Length; i++)
            {
                if (Options.ClientLimit > 0 && i + 1 >= Options.ClientLimit)
                    return SubscriptionResult.Full;

                if (_channelClients[i] == null)
                {
                    _channelClients[i] = channelClient;
                    added = true;
                    break;
                }
            }
        }

        if (!added)
            return SubscriptionResult.Full;

        client.AddSubscription(channelClient);

        foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
            _ = handler.OnSubscribe(this, client);

        Info.SubscriberCount = ClientsCount();
        Rider.Channel.SubscribeEvent.Trigger(client, Name);

        if (Options.SendLastMessageAsInitial)
        {
            byte[] msg = _initialMessage;
            if (msg != null)
                _ = client.SendRawAsync(_initialMessage);
        }

        return SubscriptionResult.Success;
    }

    private ChannelClient RemoveClientFromArray(ChannelClient client)
    {
        bool removed = false;
        ChannelClient removedClient = null;
        lock (_channelClientLock)
        {
            for (int i = 0; i < _channelClients.Length; i++)
            {
                ChannelClient cc = _channelClients[i];

                if (cc == null)
                    break;

                if (removed && i > 0)
                {
                    _channelClients[i - 1] = cc;
                    _channelClients[i] = null;
                }
                else if (cc == client)
                {
                    removedClient = cc;
                    _channelClients[i] = null;
                    removed = true;
                }
            }
        }

        return removedClient;
    }

    private ChannelClient RemoveClientFromArray(MessagingClient client)
    {
        bool removed = false;
        ChannelClient removedClient = null;
        lock (_channelClientLock)
        {
            for (int i = 0; i < _channelClients.Length; i++)
            {
                ChannelClient cc = _channelClients[i];

                if (cc == null)
                    break;

                if (removed && i > 0)
                {
                    _channelClients[i - 1] = cc;
                    _channelClients[i] = null;
                }
                else if (cc.Client == client)
                {
                    removedClient = cc;
                    _channelClients[i] = null;
                    removed = true;
                }
            }
        }

        return removedClient;
    }

    /// <summary>
    /// Removes client from the queue
    /// </summary>
    public void RemoveClient(ChannelClient client)
    {
        RemoveClientFromArray(client);
        client.Client.RemoveSubscription(client);

        foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
            _ = handler.OnUnsubscribe(this, client.Client);

        Info.SubscriberCount = ClientsCount();
        Rider.Channel.UnsubscribeEvent.Trigger(client.Client, Name);
    }

    /// <summary>
    /// Removes client from the queue, does not call MqClient's remove method
    /// </summary>
    internal void RemoveClientSilent(ChannelClient client)
    {
        RemoveClientFromArray(client);

        foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
            _ = handler.OnUnsubscribe(this, client.Client);

        Info.SubscriberCount = ClientsCount();
        Rider.Channel.UnsubscribeEvent.Trigger(client.Client, Name);
    }

    /// <summary>
    /// Removes client from the queue
    /// </summary>
    public bool RemoveClient(MessagingClient client)
    {
        ChannelClient cc = RemoveClientFromArray(client);

        if (cc == null)
            return false;

        client.RemoveSubscription(cc);

        foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
            _ = handler.OnUnsubscribe(this, client);

        Info.SubscriberCount = ClientsCount();
        Rider.Channel.UnsubscribeEvent.Trigger(client, Name);

        return true;
    }

    /// <summary>
    /// Finds client in the queue
    /// </summary>
    public ChannelClient FindClient(string uniqueId)
    {
        return _channelClients.FirstOrDefault(x => x?.Client.UniqueId == uniqueId);
    }

    /// <summary>
    /// Finds client in the queue
    /// </summary>
    public ChannelClient FindClient(MessagingClient client)
    {
        return _channelClients.FirstOrDefault(x => x?.Client == client);
    }

    #endregion
}