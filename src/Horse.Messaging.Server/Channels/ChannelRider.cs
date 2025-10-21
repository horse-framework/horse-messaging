using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using EnumsNET;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Channels;

/// <summary>
/// Manages channels in messaging server
/// </summary>
public class ChannelRider
{
    #region Properties

    private readonly ArrayContainer<HorseChannel> _channels = new();

    /// <summary>
    /// Locker object for preventing to create duplicated channels when requests are concurrent and auto channels creation is enabled
    /// </summary>
    private readonly SemaphoreSlim _createLock = new(1, 1);

    /// <summary>
    /// Event handlers to track channel events
    /// </summary>
    public ArrayContainer<IChannelEventHandler> EventHandlers { get; } = new();

    /// <summary>
    /// Channel authenticators
    /// </summary>
    public ArrayContainer<IChannelAuthorization> Authenticators { get; } = new();

    /// <summary>
    /// All Channels of the server
    /// </summary>
    public IEnumerable<HorseChannel> Channels => _channels.All();

    /// <summary>
    /// Default channel options
    /// </summary>
    public HorseChannelOptions Options { get; } = new();

    /// <summary>
    /// Root horse rider object
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Persistence configurator for routers.
    /// Settings this value to null disables the persistence for channels and they are lost after application restart.
    /// Default value is not null and saves channels into ./data/channels.json file
    /// </summary>
    public IOptionsConfigurator<ChannelConfiguration> OptionsConfigurator { get; set; }

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

    /// <summary>
    /// Channel cluster notifier
    /// </summary>
    internal ChannelClusterNotifier ClusterNotifier { get; }

    #endregion

    #region Init - Load - Save

    private bool _initializing;

    /// <summary>
    /// Creates new channel rider
    /// </summary>
    internal ChannelRider(HorseRider rider)
    {
        Rider = rider;
        ClusterNotifier = new ChannelClusterNotifier(this, rider.Cluster);
        CreateEvent = new EventManager(rider, HorseEventType.ChannelCreate);
        RemoveEvent = new EventManager(rider, HorseEventType.ChannelRemove);
        SubscribeEvent = new EventManager(rider, HorseEventType.ChannelSubscribe);
        UnsubscribeEvent = new EventManager(rider, HorseEventType.ChannelUnsubscribe);
        OptionsConfigurator = new ChannelOptionsConfigurator(rider, "channels.json");
    }

    internal void Initialize()
    {
        _initializing = true;

        if (OptionsConfigurator != null)
        {
            ChannelConfiguration[] configurations = OptionsConfigurator.Load();
            foreach (ChannelConfiguration config in configurations)
            {
                ChannelStatus status = Enums.Parse<ChannelStatus>(config.Status, true, EnumFormat.Description);
                if (status == ChannelStatus.Destroyed)
                    continue;

                HorseChannel channel = Create(config.Name, opt =>
                {
                    opt.AutoDestroy = config.AutoDestroy;
                    opt.ClientLimit = config.ClientLimit;
                    opt.MessageSizeLimit = config.MessageSizeLimit;
                }).GetAwaiter().GetResult();

                channel.Topic = config.Topic;
                channel.Status = status;
            }
        }

        _initializing = false;
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
        return Create(channelName, options, null, false, false, true);
    }

    internal async Task<HorseChannel> Create(string channelName,
        HorseChannelOptions options,
        HorseMessage requestMessage,
        bool hideException,
        bool returnIfExists,
        bool notifyCluster)
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

            if (!_initializing && OptionsConfigurator != null)
            {
                ChannelConfiguration configuration = ChannelConfiguration.Create(channel);
                OptionsConfigurator.Add(configuration);
                OptionsConfigurator.Save();
            }

            if (notifyCluster)
                ClusterNotifier.SendChannelCreated(channel);

            return channel;
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ChannelCreate, "Channel Create Error: " + channelName, e);

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
        Remove(channel, true);
    }

    internal void Remove(HorseChannel channel, bool notifyCluster)
    {
        try
        {
            _channels.Remove(channel);
            channel.Status = ChannelStatus.Destroyed;

            foreach (IChannelEventHandler handler in EventHandlers.All())
                _ = handler.OnRemoved(channel);

            RemoveEvent.Trigger(channel.Name);
            channel.Destroy();

            if (OptionsConfigurator != null)
            {
                OptionsConfigurator.Remove(x => x.Name == channel.Name);
                OptionsConfigurator.Save();
            }

            if (notifyCluster)
                ClusterNotifier.SendChannelRemoved(channel);
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ChannelRemove, "Channel Remove Error: " + channel?.Name, e);
        }
    }

    /// <summary>
    /// If channel options changed by Options property.
    /// Applies new configurationsa and triggers sync operations between nodes. 
    /// </summary>
    public void ApplyChangedOptions(HorseChannel channel)
    {
        ClusterNotifier.SendChannelUpdated(channel);
    }

    #endregion
}