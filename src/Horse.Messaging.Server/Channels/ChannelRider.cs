using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Containers;
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

        #endregion

        /// <summary>
        /// Creates new channel rider
        /// </summary>
        internal ChannelRider(HorseRider rider)
        {
            Rider = rider;
        }

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

        internal async Task<HorseChannel> Create(string channelName,
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

                //OnQueueCreated.Trigger(queue);
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

                //OnQueueRemoved.Trigger(queue);
                channel.Destroy();
            }
            catch (Exception e)
            {
                Rider.SendError("REMOVE_CHANNEL", e, $"ChannelName:{channel?.Name}");
            }
        }

        #endregion
    }
}