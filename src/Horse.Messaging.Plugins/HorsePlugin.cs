using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Plugins;

public abstract class HorsePlugin
{
    public string Name { get; protected set; }
    public string Description { get; protected set; }

    public void AddTimer(TimeSpan interval, IHorsePluginHandler handler)
    {
        throw new NotImplementedException();
    }

    public void AddChannelPublish(string channelName, IHorsePluginHandler handler)
    {
        throw new NotImplementedException();
    }

    public void AddQueuePush(string queueName, IHorsePluginHandler handler)
    {
        throw new NotImplementedException();
    }

    public void AddRouterPublish(string routerName, IHorsePluginHandler handler)
    {
        throw new NotImplementedException();
    }

    public void AddRequestHandler(IHorsePluginHandler handler)
    {
        throw new NotImplementedException();
    }

    public async Task SendMessage(HorseMessage message)
    {
        throw new NotImplementedException();
    }
}