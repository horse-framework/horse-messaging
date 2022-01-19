using System;
using System.Collections.Generic;
using System.Threading;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Channels
{
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
        /// Clients in the queue as cloned list
        /// </summary>
        public IEnumerable<ChannelClient> Clients => _clients.All();

        private readonly ArrayContainer<ChannelClient> _clients = new ArrayContainer<ChannelClient>();
        private Timer _destoryTimer;

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

            _destoryTimer = new Timer(s =>
            {
                if (Options.AutoDestroy &&
                    DateTime.UtcNow - LastPublishDate > TimeSpan.FromSeconds(30) &&
                    _clients.Count() == 0)
                {
                    Rider.Channel.Remove(this);
                }
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

            try
            {
                byte[] messageData = HorseProtocolWriter.Create(message);

                int count = 0;
                //to all receivers
                foreach (ChannelClient client in Clients)
                {
                    //to only online receivers
                    if (!client.Client.IsConnected)
                        continue;

                    //send the message
                    _ = client.Client.SendAsync(messageData);
                    count++;
                }

                lock (Info)
                {
                    Info.Published++;
                    Info.Received += count;
                }

                PublishEvent.Trigger(Name);

                foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
                    _ = handler.OnPublish(this, message);

                return PushResult.Success;
            }
            catch (Exception ex)
            {
                Rider.SendError("PUSH", ex, $"ChannelName:{Name}");
                return PushResult.Error;
            }
        }

        #endregion

        #region Client Actions

        /// <summary>
        /// Returns client count in the queue
        /// </summary>
        /// <returns></returns>
        public int ClientsCount()
        {
            return _clients.Count();
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

            if (Options.ClientLimit > 0 && _clients.Count() >= Options.ClientLimit)
                return SubscriptionResult.Full;

            ChannelClient cc = new ChannelClient(this, client);
            _clients.Add(cc);
            client.AddSubscription(cc);

            foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
                _ = handler.OnSubscribe(this, client);

            Info.SubscriberCount = _clients.Count();
            Rider.Channel.SubscribeEvent.Trigger(client, Name);
            return SubscriptionResult.Success;
        }

        /// <summary>
        /// Removes client from the queue
        /// </summary>
        public void RemoveClient(ChannelClient client)
        {
            _clients.Remove(client);
            client.Client.RemoveSubscription(client);

            foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
                _ = handler.OnUnsubscribe(this, client.Client);

            Info.SubscriberCount = _clients.Count();
            Rider.Channel.UnsubscribeEvent.Trigger(client.Client, Name);
        }

        /// <summary>
        /// Removes client from the queue, does not call MqClient's remove method
        /// </summary>
        internal void RemoveClientSilent(ChannelClient client)
        {
            _clients.Remove(client);

            foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
                _ = handler.OnUnsubscribe(this, client.Client);

            Info.SubscriberCount = _clients.Count();
            Rider.Channel.UnsubscribeEvent.Trigger(client.Client, Name);
        }

        /// <summary>
        /// Removes client from the queue
        /// </summary>
        public bool RemoveClient(MessagingClient client)
        {
            ChannelClient cc = _clients.Find(x => x.Client == client);

            if (cc == null)
                return false;

            _clients.Remove(cc);
            client.RemoveSubscription(cc);

            foreach (IChannelEventHandler handler in Rider.Channel.EventHandlers.All())
                _ = handler.OnUnsubscribe(this, client);

            Info.SubscriberCount = _clients.Count();
            Rider.Channel.UnsubscribeEvent.Trigger(client, Name);

            return true;
        }

        /// <summary>
        /// Finds client in the queue
        /// </summary>
        public ChannelClient FindClient(string uniqueId)
        {
            return _clients.Find(x => x.Client.UniqueId == uniqueId);
        }

        /// <summary>
        /// Finds client in the queue
        /// </summary>
        public ChannelClient FindClient(MessagingClient client)
        {
            return _clients.Find(x => x.Client == client);
        }

        #endregion
    }
}