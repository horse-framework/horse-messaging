using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Channels
{
    /// <summary>
    /// Manages channels in messaging server
    /// </summary>
    public class ChannelRider
    {
        #region Properties

        private readonly ArrayContainer<HorseChannel> _channels = new ArrayContainer<HorseChannel>();

        /// <summary>
        /// Locker object for preventing to create duplicated channels when requests are concurrent and auto channels creation is enabled
        /// </summary>
        private readonly SemaphoreSlim _createLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Event handlers to track channel events
        /// </summary>
        public ArrayContainer<IChannelEventHandler> EventHandlers { get; } = new ArrayContainer<IChannelEventHandler>();

        /// <summary>
        /// Channel authenticators
        /// </summary>
        public ArrayContainer<IChannelAuthorization> Authenticators { get; } = new ArrayContainer<IChannelAuthorization>();

        /// <summary>
        /// All Channels of the server
        /// </summary>
        public IEnumerable<HorseChannel> Channels => _channels.All();

        /// <summary>
        /// Default channel options
        /// </summary>
        public HorseChannelOptions Options { get; } = new HorseChannelOptions();

        /// <summary>
        /// Root horse rider object
        /// </summary>
        public HorseRider Rider { get; }

        /// <summary>
        /// Event Manager for HorseEventType.ChannelCreate
        /// </summary>
        public EventManager CreateEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.ChannelRemove
        /// </summary>
        public EventManager RemoveEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.ChannelSubscribe
        /// </summary>
        public EventManager SubscribeEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.ChannelUnsubscribe
        /// </summary>
        public EventManager UnsubscribeEvent { get; }

        #endregion

        #region Init - Load - Save

        private bool _initializing;

        /// <summary>
        /// Creates new channel rider
        /// </summary>
        internal ChannelRider(HorseRider rider)
        {
            Rider = rider;
            CreateEvent = new EventManager(rider, HorseEventType.ChannelCreate);
            RemoveEvent = new EventManager(rider, HorseEventType.ChannelRemove);
            SubscribeEvent = new EventManager(rider, HorseEventType.ChannelSubscribe);
            UnsubscribeEvent = new EventManager(rider, HorseEventType.ChannelUnsubscribe);
        }

        internal void Initialize()
        {
            string fullpath = $"{Rider.Options.DataPath}/channels.json";
            if (!System.IO.File.Exists(fullpath))
            {
                GlobalChannelConfigData g = new GlobalChannelConfigData();
                g.Channels = new List<ChannelConfigData>();
                System.IO.File.WriteAllText(fullpath, JsonSerializer.Serialize(g));
                return;
            }

            _initializing = true;
            string json = System.IO.File.ReadAllText(fullpath);
            GlobalChannelConfigData global = JsonSerializer.Deserialize<GlobalChannelConfigData>(json);

            foreach (ChannelConfigData definition in global.Channels)
            {
                ChannelStatus status = definition.Status.ToChannelStatus();
                if (status == ChannelStatus.Destroyed)
                    continue;

                HorseChannel channel = Create(definition.Name, opt =>
                {
                    opt.AutoDestroy = definition.AutoDestroy;
                    opt.ClientLimit = definition.ClientLimit;
                    opt.MessageSizeLimit = definition.MessageSizeLimit;
                }).GetAwaiter().GetResult();

                channel.Topic = definition.Topic;
                channel.Status = status;
            }

            _initializing = false;
        }

        internal void SaveChannels()
        {
            if (_initializing)
                return;

            GlobalChannelConfigData global = new GlobalChannelConfigData();
            global.AutoDestroy = Options.AutoDestroy;
            global.AutoChannelCreation = Options.AutoChannelCreation;
            global.ClientLimit = Options.ClientLimit;
            global.MessageSizeLimit = Options.MessageSizeLimit;
            global.Channels = new List<ChannelConfigData>();

            foreach (HorseChannel channel in _channels.All())
            {
                if (channel.Status == ChannelStatus.Destroyed)
                    continue;
                
                ChannelConfigData configData = new ChannelConfigData
                {
                    Name = channel.Name,
                    Status = channel.Status.ToString(),
                    Topic = channel.Topic,
                    AutoDestroy = channel.Options.AutoDestroy,
                    ClientLimit = channel.Options.ClientLimit,
                    MessageSizeLimit = channel.Options.MessageSizeLimit
                };

                global.Channels.Add(configData);
            }

            try
            {
                string json = JsonSerializer.Serialize(global);
                System.IO.File.WriteAllText($"{Rider.Options.DataPath}/channels.json", json);
            }
            catch (Exception e)
            {
                Rider.SendError("SaveChannels", e, null);
            }
        }

        #endregion

        #region Actions

        /// <summary>
        /// Finds channel by name
        /// </summary>
        public HorseChannel Find(string name)
        {
            return _channels.Find(x => x.Name == name);
        }

        /// <summary>
        /// Creates new channel with default options and default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when channel limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a channel with same id</exception>
        public Task<HorseChannel> Create(string channelName)
        {
            HorseChannelOptions options = HorseChannelOptions.Clone(Options);
            return Create(channelName, options);
        }

        /// <summary>
        /// Creates new channel with default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when channel limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a channel with same id</exception>
        public Task<HorseChannel> Create(string channelName, Action<HorseChannelOptions> optionsAction)
        {
            HorseChannelOptions options = HorseChannelOptions.Clone(Options);
            optionsAction(options);
            return Create(channelName, options);
        }

        /// <summary>
        /// Creates new channel
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when channel limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a channel with same id</exception>
        public Task<HorseChannel> Create(string channelName, HorseChannelOptions options)
        {
            return Create(channelName, options, null, false, false);
        }

        private async Task<HorseChannel> Create(string channelName,
            HorseChannelOptions options,
            HorseMessage requestMessage,
            bool hideException,
            bool returnIfExists)
        {
            await _createLock.WaitAsync();
            try
            {
                if (!Filter.CheckNameEligibility(channelName))
                    throw new InvalidOperationException("Invalid channel name");

                if (Rider.Options.ChannelLimit > 0 && Rider.Options.ChannelLimit >= _channels.Count())
                    throw new OperationCanceledException("Channel limit is exceeded for the server");

                HorseChannel channel = _channels.Find(x => x.Name == channelName);

                if (channel != null)
                {
                    if (returnIfExists)
                        return channel;

                    throw new DuplicateNameException($"The server has already a channel with same name: {channelName}");
                }

                channel = new HorseChannel(Rider, channelName, options);
                if (requestMessage != null)
                    channel.UpdateOptionsByMessage(requestMessage);

                _channels.Add(channel);
                foreach (IChannelEventHandler handler in EventHandlers.All())
                    _ = handler.OnCreated(channel);

                CreateEvent.Trigger(channelName);
                SaveChannels();
                return channel;
            }
            catch (Exception e)
            {
                Rider.SendError("CREATE_CHANNEL", e, $"ChannelName:{channelName}");

                if (!hideException)
                    throw;

                return null;
            }
            finally
            {
                try
                {
                    _createLock.Release();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Removes a channel from the server
        /// </summary>
        public void Remove(string name)
        {
            HorseChannel channel = _channels.Find(x => x.Name == name);
            if (channel == null)
                return;

            Remove(channel);
        }

        /// <summary>
        /// Removes a channel from the server
        /// </summary>
        public void Remove(HorseChannel channel)
        {
            try
            {
                _channels.Remove(channel);
                channel.Status = ChannelStatus.Destroyed;

                foreach (IChannelEventHandler handler in EventHandlers.All())
                    _ = handler.OnRemoved(channel);

                RemoveEvent.Trigger(channel.Name);
                channel.Destroy();
                SaveChannels();
            }
            catch (Exception e)
            {
                Rider.SendError("REMOVE_CHANNEL", e, $"ChannelName:{channel?.Name}");
            }
        }

        #endregion
    }
}