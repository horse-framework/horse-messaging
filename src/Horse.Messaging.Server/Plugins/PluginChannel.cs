using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Plugins;
using Horse.Messaging.Plugins.Channels;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Channels;

namespace Horse.Messaging.Server.Plugins;

internal class PluginChannel : IPluginChannel
{
    private readonly HorseChannel _channel;

    public string Name => _channel.Name;
    public string Topic => _channel.Topic;

    public PluginChannelStatus Status
    {
        get => (PluginChannelStatus)_channel.Status;
        set => _channel.Status = (ChannelStatus)value;
    }

    public PluginChannel(HorseChannel channel)
    {
        _channel = channel;
    }

    public PluginPushResult Push(string message)
    {
        return (PluginPushResult)_channel.Push(message);
    }

    public PluginPushResult Push(HorseMessage message)
    {
        return (PluginPushResult)_channel.Push(message);
    }

    public PluginChannelOptions GetOptions()
    {
        return CreateFrom(_channel.Options);
    }

    public void SetOptions(Action<PluginChannelOptions> options)
    {
        var o = CreateFrom(_channel.Options);
        options(o);
        ApplyTo(o, _channel.Options);
    }

    public Task<HorseMessage> GetInitialMessage()
    {
        return _channel.GetInitialMessage();
    }

    public ChannelInformation GetInfo()
    {
        return new ChannelInformation
        {
            Name = _channel.Name,
            Status = _channel.Status.ToString(),
            Topic = _channel.Topic,
            Published = _channel.Info.Published,
            PublishedValue = _channel.Info.PublishedValue,
            Received = _channel.Info.Received,
            ReceivedValue = _channel.Info.ReceivedValue,
            SubscriberCount = _channel.ClientsCount()
        };
    }

    public IEnumerable<IPluginMessagingClient> GetSubscribers()
    {
        return _channel.Clients.Select(x => new PluginMessagingClient(x.Client));
    }

    internal static PluginChannelOptions CreateFrom(HorseChannelOptions options)
    {
        return new PluginChannelOptions
        {
            AutoDestroy = options.AutoDestroy,
            ClientLimit = options.ClientLimit,
            MessageSizeLimit = options.MessageSizeLimit,
            AutoChannelCreation = options.AutoChannelCreation,
            AutoDestroyIdleSeconds = options.AutoDestroyIdleSeconds,
            SendLastMessageAsInitial = options.SendLastMessageAsInitial
        };
    }

    internal static void ApplyTo(PluginChannelOptions options, HorseChannelOptions target)
    {
        target.AutoDestroy = options.AutoDestroy;
        target.ClientLimit = options.ClientLimit;
        target.MessageSizeLimit = options.MessageSizeLimit;
        target.AutoChannelCreation = options.AutoChannelCreation;
        target.AutoDestroyIdleSeconds = options.AutoDestroyIdleSeconds;
        target.SendLastMessageAsInitial = options.SendLastMessageAsInitial;
    }
}