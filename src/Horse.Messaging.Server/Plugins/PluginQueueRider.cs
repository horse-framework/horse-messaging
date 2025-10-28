using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Plugins.Queues;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Plugins;

internal class PluginQueueRider : IPluginQueueRider
{
    private readonly HorseRider _rider;

    public PluginQueueRider(HorseRider rider)
    {
        _rider = rider;
    }

    public IEnumerable<IPluginQueue> GetAll()
    {
        return _rider.Queue.Queues.Select(x => new PluginQueue(x));
    }

    public IPluginQueue Find(string name)
    {
        return new PluginQueue(_rider.Queue.Find(name));
    }

    public async Task<IPluginQueue> Create(string name)
    {
        HorseQueue queue = await _rider.Queue.Create(name);
        return queue != null ? new PluginQueue(queue) : null;
    }

    public async Task<IPluginQueue> Create(string name, Action<PluginQueueOptions> options)
    {
        QueueOptions queueOptions = QueueOptions.CloneFrom(_rider.Queue.Options);
        PluginQueueOptions pluginOptions = PluginQueue.CreateFrom(queueOptions);

        options(pluginOptions);
        PluginQueue.ApplyTo(pluginOptions, queueOptions);

        HorseQueue queue = await _rider.Queue.Create(name, queueOptions);
        return queue != null ? new PluginQueue(queue) : null;
    }

    public Task Remove(string name)
    {
        return _rider.Queue.Remove(name);
    }

    public PluginQueueOptions GetDefaultOptions()
    {
        return PluginQueue.CreateFrom(_rider.Queue.Options);
    }

    public void SetDefaultOptions(Action<PluginQueueOptions> options)
    {
        PluginQueueOptions opt = PluginQueue.CreateFrom(_rider.Queue.Options);
        options(opt);
        PluginQueue.ApplyTo(opt, _rider.Queue.Options);
    }
}