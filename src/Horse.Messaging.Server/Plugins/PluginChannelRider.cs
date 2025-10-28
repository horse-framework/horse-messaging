using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Plugins.Channels;
using Horse.Messaging.Server.Channels;

namespace Horse.Messaging.Server.Plugins;

internal class PluginChannelRider : IPluginChannelRider
{
    private readonly HorseRider _rider;

    public PluginChannelRider(HorseRider rider)
    {
        _rider = rider;
    }

    public IEnumerable<IPluginChannel> GetAll()
    {
        return _rider.Channel.Channels.Select(x => new PluginChannel(x));
    }

    public IPluginChannel Find(string name)
    {
        return new PluginChannel(_rider.Channel.Find(name));
    }

    public async Task<IPluginChannel> Create(string name)
    {
        var channel = await _rider.Channel.Create(name);
        if (channel == null)
            return null;

        return new PluginChannel(channel);
    }

    public async Task<IPluginChannel> Create(string name, Action<PluginChannelOptions> options)
    {
        PluginChannelOptions opt = PluginChannel.CreateFrom(_rider.Channel.Options);
        options(opt);

        HorseChannelOptions channelOptions = new HorseChannelOptions();
        PluginChannel.ApplyTo(opt, channelOptions);

        HorseChannel channel = await _rider.Channel.Create(name, channelOptions);
        return channel == null ? null : new PluginChannel(channel);
    }

    public void Remove(string name)
    {
        _rider.Channel.Remove(name);
    }

    public PluginChannelOptions GetDefaultOptions()
    {
        return PluginChannel.CreateFrom(_rider.Channel.Options);
    }

    public void SetDefaultOptions(Action<PluginChannelOptions> options)
    {
        PluginChannelOptions o = PluginChannel.CreateFrom(_rider.Channel.Options);
        options(o);
        PluginChannel.ApplyTo(o, _rider.Channel.Options);
    }
}