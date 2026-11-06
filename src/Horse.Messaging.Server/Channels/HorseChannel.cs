using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
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
    private static readonly HashSet<string> OperationalHeadersToRemove = new(StringComparer.OrdinalIgnoreCase)
    {
        HorseHeaders.CHANNEL_NAME,
        HorseHeaders.CC,
        HorseHeaders.CLIENT_LIMIT
    };

    #region Properties

    /// <summary>
    /// Unique name (not case-sensetive)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Channel topic
    /// </summary>
    public string Topic
    {
        get => _topic;
        set
        {
            _topic = value;
            Info.Topic = value;
        }
    }

    /// <summary>
    /// Root horse rider
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Channel status
    /// </summary>
    public ChannelStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            Info.Status = value.ToString();
        }
    }

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
    public IEnumerable<ChannelClient> Clients => _channelClients;

    private string _topic;
    private volatile ChannelStatus _status;
    private Timer _destroyTimer;
    private byte[] _initialMessage;
    private readonly object _channelClientLock = new();

    /// <summary>
    /// Subscribers of the channel. Copy-on-write: the array is never mutated after it is published,
    /// writers replace it under _channelClientLock, so readers (delivery loop) can iterate it lock-free
    /// without skipping or duplicating clients while subscriptions change.
    /// </summary>
    private volatile ChannelClient[] _channelClients = [];

    /// <summary>
    /// UTC ticks of the last publish, subscribe, unsubscribe or creation. Used for auto destroy idle time.
    /// </summary>
    private long _lastActivityTicks;

    private readonly Channel<HorseMessage> _channel = Channel.CreateBounded<HorseMessage>(
        new BoundedChannelOptions(50000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    #endregion

    #region Constructors - Destroy

    internal HorseChannel(HorseRider rider, string name, HorseChannelOptions options)
    {
        Rider = rider;
        Name = name;
        Options = options;
        //publish event subscriptions belong to this channel instance and end with it (see Destroy)
        PublishEvent = new EventManager(rider, HorseEventType.ChannelPublish, name, false);

        //created before Status is set, Topic and Status setters keep Info up to date
        Info = new ChannelInformation { Name = name };
        Status = ChannelStatus.Running;

        _lastActivityTicks = DateTime.UtcNow.Ticks;
        _destroyTimer = new Timer(_ => CheckAutoDestroy(), null, 15000, 15000);

        _ = Run();
    }

    internal bool CanSubscribe(MessagingClient client)
    {
        foreach (IChannelAuthorization authenticator in Rider.Channel.Authenticators.All())
        {
            bool allowed = authenticator.CanSubscribe(this, client);
            if (!allowed)
                return false;
        }

        return true;
    }

    private void UpdateLastActivity()
    {
        UpdateLastActivity(DateTime.UtcNow);
    }

    /// <summary>
    /// A plain atomic write, no fence: only the auto destroy timer reads it and a slightly stale value is fine.
    /// </summary>
    private void UpdateLastActivity(DateTime now)
    {
        Volatile.Write(ref _lastActivityTicks, now.Ticks);
    }

    internal void CheckAutoDestroy()
    {
        try
        {
            if (!Options.AutoDestroy)
                return;

            lock (_channelClientLock)
            {
                if (Status == ChannelStatus.Destroyed || _channelClients.Length > 0)
                    return;

                DateTime lastActivity = new DateTime(Volatile.Read(ref _lastActivityTicks), DateTimeKind.Utc);
                if (DateTime.UtcNow - lastActivity < TimeSpan.FromSeconds(Options.AutoDestroyIdleSeconds))
                    return;

                //marked under the client lock, a concurrent subscription cannot slip into a channel that is being removed
                Status = ChannelStatus.Destroyed;
            }

            //Idle and subscriber count are this node's own state, other nodes decide for their copies themselves.
            //Not sent to the cluster: a copy with no local subscribers would remove the channel on the nodes it is in use.
            Rider.Channel.Remove(this, false);
        }
        catch (Exception e)
        {
            //timer callbacks must not throw, an unhandled exception there terminates the process
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ChannelRemove, "Channel Auto Destroy Error: " + Name, e);
        }
    }

    internal void Destroy()
    {
        ChannelClient[] clients;
        lock (_channelClientLock)
        {
            Status = ChannelStatus.Destroyed;
            clients = _channelClients;
            _channelClients = [];
            Info.SubscriberCount = 0;
        }

        //subscriptions end with the channel. Otherwise clients keep a dead subscription,
        //and do not receive anything from a new channel that is created with the same name.
        foreach (ChannelClient cc in clients)
        {
            cc.Client.RemoveSubscription(cc);
            cc.Dispose();
        }

        try
        {
            _channel.Writer.Complete();
        }
        catch
        {
        }

        if (_destroyTimer != null)
        {
            _destroyTimer.Dispose();
            _destroyTimer = null;
        }

        //publish event subscriptions end with the channel too, like the subscribers above.
        //A channel created later with the same name starts without them.
        PublishEvent.Dispose();
    }

    internal bool UpdateOptionsByMessage(HorseMessage message)
    {
        bool changed = false;

        string clientLimit = message.FindHeader(HorseHeaders.CLIENT_LIMIT);
        if (!string.IsNullOrEmpty(clientLimit))
        {
            int value = Convert.ToInt32(clientLimit.Trim());
            if (Options.ClientLimit != value)
            {
                Options.ClientLimit = value;
                changed = true;
            }
        }

        string messageSizeLimit = message.FindHeader(HorseHeaders.MESSAGE_SIZE_LIMIT);
        if (!string.IsNullOrEmpty(messageSizeLimit))
        {
            ulong value = Convert.ToUInt64(messageSizeLimit.Trim());
            if (Options.MessageSizeLimit != value)
            {
                Options.MessageSizeLimit = value;
                changed = true;
            }
        }

        string autoDestroy = message.FindHeader(HorseHeaders.AUTO_DESTROY);
        if (!string.IsNullOrEmpty(autoDestroy))
        {
            string value = autoDestroy.Trim();
            bool parsed = value.Equals("TRUE", StringComparison.CurrentCultureIgnoreCase) || value == "1";
            if (Options.AutoDestroy != parsed)
            {
                Options.AutoDestroy = parsed;
                changed = true;
            }
        }

        string initialMessage = message.FindHeader(HorseHeaders.CHANNEL_INITIAL_MESSAGE);
        if (!string.IsNullOrEmpty(initialMessage))
        {
            string value = initialMessage.Trim();
            bool parsed = value.Equals("TRUE", StringComparison.CurrentCultureIgnoreCase) || value == "1";
            if (Options.SendLastMessageAsInitial != parsed)
            {
                Options.SendLastMessageAsInitial = parsed;
                changed = true;
            }
        }

        string idleSeconds = message.FindHeader(HorseHeaders.CHANNEL_DESTROY_IDLE_SECONDS);
        if (!string.IsNullOrEmpty(idleSeconds))
        {
            int value = Convert.ToInt32(idleSeconds.Trim());
            if (Options.AutoDestroyIdleSeconds != value)
            {
                Options.AutoDestroyIdleSeconds = value;
                changed = true;
            }
        }

        return changed;
    }

    private async Task Run()
    {
        await foreach (HorseMessage message in _channel.Reader.ReadAllAsync())
        {
            try
            {
                byte[] messageData = HorseProtocolWriter.Create(message);

                //last message and the receiver list are taken together. A subscriber added before this point
                //gets the previous message as initial and this one through the loop below, a subscriber added
                //after gets this one as initial. So it never receives them out of order or twice.
                //One uncontended lock per message, not per client.
                ChannelClient[] clients;
                lock (_channelClientLock)
                {
                    _initialMessage = messageData;
                    clients = _channelClients;
                }

                int count = 0;
                foreach (ChannelClient client in clients)
                {
                    //to only online receivers
                    if (!client.Client.IsConnected)
                        continue;

                    client.SendChannelMessage(messageData);
                    count++;
                }

                Info.PublishedValue++;
                Info.ReceivedValue += count;

                PublishEvent.Trigger(Name);

                foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
                    _ = handler.OnPublish(this, message);

                Rider.Plugin.TriggerPluginHandlers(HorsePluginEvent.ChannelPublish, Name, message);
            }
            catch (Exception ex)
            {
                Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ChannelPush, "Channel Push Error: " + Name, ex);
            }
        }
    }

    #endregion

    #region Delivery

    /// <summary>
    /// Pushes new message into the queue
    /// </summary>
    public PushResult Push(string message)
    {
        HorseMessage msg = new HorseMessage(MessageType.Channel, Name, KnownContentTypes.ChannelPush);
        msg.SetStringContent(message);
        return Push(msg);
    }

    /// <summary>
    /// Pushes a message into the queue.
    /// </summary>
    internal PushResult Push(HorseMessage message)
    {
        if (Status == ChannelStatus.Paused || Status == ChannelStatus.Destroyed)
            return PushResult.StatusNotSupported;

        if (Options.MessageSizeLimit > 0 && message.Length > Options.MessageSizeLimit)
            return PushResult.LimitExceeded;

        //remove operational headers that are should not be sent to consumers or saved to disk
        message.RemoveHeaders(OperationalHeadersToRemove);
        message.WaitResponse = false;
        //one clock read per publish, shared by the publish date and the auto destroy activity time
        DateTime now = DateTime.UtcNow;
        LastPublishDate = now;
        UpdateLastActivity(now);

        bool result = _channel.Writer.TryWrite(message);

        return result ? PushResult.Success : PushResult.LimitExceeded;
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
        return _channelClients.Length;
    }

    /// <summary>
    /// Adds the client to the queue
    /// </summary>
    public SubscriptionResult AddClient(MessagingClient client)
    {
        return AddClient(client, false);
    }

    internal SubscriptionResult AddClient(MessagingClient client, bool authorizationChecked)
    {
        if (!authorizationChecked && !CanSubscribe(client))
            return SubscriptionResult.Unauthorized;

        ChannelClient channelClient;
        lock (_channelClientLock)
        {
            //destroyed (or being destroyed by auto destroy) channel cannot accept subscribers
            if (Status == ChannelStatus.Destroyed)
                return SubscriptionResult.Full;

            ChannelClient[] clients = _channelClients;

            //subscribing twice is a no-op, a client must not take two slots and receive each message twice
            foreach (ChannelClient existing in clients)
            {
                if (existing.Client == client)
                    return SubscriptionResult.Success;
            }

            if (Options.ClientLimit > 0 && clients.Length >= Options.ClientLimit)
                return SubscriptionResult.Full;

            channelClient = new ChannelClient(this, client);

            //queued before the client becomes visible to the delivery loop, see Run()
            if (Options.SendLastMessageAsInitial)
            {
                byte[] initialMessage = _initialMessage;
                if (initialMessage != null)
                    channelClient.SendChannelMessage(initialMessage);
            }

            ChannelClient[] next = new ChannelClient[clients.Length + 1];
            Array.Copy(clients, next, clients.Length);
            next[clients.Length] = channelClient;
            _channelClients = next;

            //updated with the array under the lock, concurrent changes cannot leave a stale count
            Info.SubscriberCount = next.Length;
        }

        UpdateLastActivity();
        client.AddSubscription(channelClient);
        _ = channelClient.Run();

        foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
            _ = handler.OnSubscribe(this, client);

        Rider.Channel.SubscribeEvent.Trigger(client, Name);

        return SubscriptionResult.Success;
    }

    /// <summary>
    /// Removes the first client that matches from the subscribers array and returns it.
    /// Returns null if there is no matching client.
    /// </summary>
    private ChannelClient RemoveClientFromArray(Predicate<ChannelClient> match)
    {
        ChannelClient removed;
        lock (_channelClientLock)
        {
            ChannelClient[] clients = _channelClients;
            int index = Array.FindIndex(clients, match);
            if (index < 0)
                return null;

            removed = clients[index];
            ChannelClient[] next = new ChannelClient[clients.Length - 1];
            Array.Copy(clients, 0, next, 0, index);
            Array.Copy(clients, index + 1, next, index, clients.Length - index - 1);
            _channelClients = next;
            Info.SubscriberCount = next.Length;
        }

        UpdateLastActivity();
        return removed;
    }

    /// <summary>
    /// Completes the removal of a client that is taken out of the subscribers array
    /// </summary>
    private void OnClientRemoved(ChannelClient client)
    {
        client.Dispose();

        foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
            _ = handler.OnUnsubscribe(this, client.Client);

        Rider.Channel.UnsubscribeEvent.Trigger(client.Client, Name);
    }

    /// <summary>
    /// Removes client from the queue
    /// </summary>
    public void RemoveClient(ChannelClient client)
    {
        ChannelClient removed = RemoveClientFromArray(x => x == client);
        client.Client.RemoveSubscription(client);

        if (removed != null)
            OnClientRemoved(removed);
        else
            client.Dispose();
    }

    /// <summary>
    /// Removes client from the queue, does not call MqClient's remove method
    /// </summary>
    internal void RemoveClientSilent(ChannelClient client)
    {
        ChannelClient removed = RemoveClientFromArray(x => x == client);

        if (removed != null)
            OnClientRemoved(removed);
        else
            client.Dispose();
    }

    /// <summary>
    /// Removes client from the queue
    /// </summary>
    public bool RemoveClient(MessagingClient client)
    {
        ChannelClient cc = RemoveClientFromArray(x => x.Client == client);

        if (cc == null)
            return false;

        client.RemoveSubscription(cc);
        OnClientRemoved(cc);
        return true;
    }

    /// <summary>
    /// Finds client in the queue
    /// </summary>
    public ChannelClient FindClient(string uniqueId)
    {
        return Array.Find(_channelClients, x => x.Client.UniqueId == uniqueId);
    }

    /// <summary>
    /// Finds client in the queue
    /// </summary>
    public ChannelClient FindClient(MessagingClient client)
    {
        return Array.Find(_channelClients, x => x.Client == client);
    }

    #endregion
}
