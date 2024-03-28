﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Plugins;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;

namespace Horse.Messaging.Server.Plugins;

/// <inheritdoc />
public class PluginRider : IPluginRider
{
    /// <inheritdoc />
    public int ServerPort { get; set; }

    /// <summary>
    /// Implemented Plugins
    /// </summary>
    public HorsePlugin[] Plugins { get; private set; } = Array.Empty<HorsePlugin>();

    private readonly List<PluginAssemblyData> _data = new List<PluginAssemblyData>();
    private readonly HorseRider _rider;
    private readonly string _dataFilename = "plugins.json";

    internal PluginRider(HorseRider rider)
    {
        ServerPort = rider.Server.Options.Hosts.FirstOrDefault(x => !x.SslEnabled)?.Port ?? 0;
        _rider = rider;
    }

    /// <summary>
    /// Loads all plugin assemblies and initializes all plugins
    /// </summary>
    public void Initialize()
    {
        LoadData();

        lock (_data)
        {
            foreach (PluginAssemblyData data in _data)
            {
                if (data.Plugins.All(x => x.Disabled))
                    continue;

                LoadAssemblyPlugins(data).GetAwaiter().GetResult();
            }
        }
    }

    private void LoadData()
    {
        string fullname = $"{_rider.Options.DataPath}/{_dataFilename}";

        if (!File.Exists(fullname))
        {
            File.WriteAllText(fullname, "[]");

            lock (_data)
                _data.Clear();
        }

        string json = File.ReadAllText(fullname);
        var data = JsonSerializer.Deserialize<List<PluginAssemblyData>>(json, SerializerFactory.Default(true, true));

        lock (_data)
        {
            _data.Clear();
            _data.AddRange(data);
        }
    }

    private void SaveData()
    {
        lock (_data)
        {
            string json = JsonSerializer.Serialize(_data, SerializerFactory.Default(true, true));
            File.WriteAllText($"{_rider.Options.DataPath}/{_dataFilename}", json);
        }
    }

    /// <summary>
    /// Loads all IHorsePluginBuilder types from assembly and adds all plugins
    /// </summary>
    private async Task LoadAssemblyPlugins(PluginAssemblyData data)
    {
        Assembly assembly = Assembly.LoadFrom(data.Location);
        data.LoadedAssembly = assembly;

        foreach (Type type in GetPluginBuilderTypesOfAssembly(assembly))
        {
            try
            {
                IHorsePluginBuilder builder = (IHorsePluginBuilder) Activator.CreateInstance(type);
                PluginData pluginData = data.Plugins.FirstOrDefault(x => x.FullTypeName == type.FullName);

                if (pluginData != null && pluginData.Removed)
                    continue;

                HorsePlugin plugin;
                if (pluginData == null)
                {
                    plugin = builder.Build();
                    if (Plugins.Any(x => string.Equals(x.Name, plugin.Name)))
                    {
                        _ = plugin.Remove();
                        continue;
                    }

                    pluginData = new PluginData
                    {
                        Name = plugin.Name,
                        FullTypeName = type.FullName,
                        Removed = false,
                        Disabled = false
                    };

                    data.Plugins.Add(pluginData);
                }
                else
                    plugin = builder.Build();

                plugin.Set(this);
                await plugin.Initialize();

                plugin.Initialized = true;
                plugin.Removed = false;

                var plugins = Plugins.ToList();
                plugins.Add(plugin);
                Plugins = plugins.ToArray();
            }
            catch (Exception e)
            {
                _rider.SendError("Plugin", e, type.FullName);
            }
        }
    }

    /// <summary>
    /// Loads all IHorsePluginBuilder types from assembly and adds all plugins
    /// </summary>
    public async Task AddAssemblyPlugins(string filename)
    {
        Assembly assembly = Assembly.LoadFrom(filename);
        PluginAssemblyData assemblyData = null;
        foreach (Type type in assembly.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            if (typeof(IHorsePluginBuilder).IsAssignableFrom(type))
            {
                if (assemblyData == null)
                {
                    lock (_data)
                    {
                        assemblyData = _data.FirstOrDefault(x => x.Fullname == assembly.FullName);
                        if (assemblyData == null)
                        {
                            assemblyData = new PluginAssemblyData
                            {
                                Filename = filename,
                                Location = assembly.Location,
                                Fullname = assembly.FullName,
                                Plugins = new List<PluginData>(),
                                AssemblyVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion
                            };
                            _data.Add(assemblyData);
                        }
                    }

                    assemblyData.LoadedAssembly = assembly;
                }

                try
                {
                    IHorsePluginBuilder builder = (IHorsePluginBuilder) Activator.CreateInstance(type);
                    await AddPlugin(assemblyData, builder);
                }
                catch (Exception e)
                {
                    _rider.SendError("Plugin", e, type.FullName);
                }
            }
        }
    }

    /// <summary>
    /// Adds plugin
    /// </summary>
    private async Task AddPlugin(PluginAssemblyData assemblyData, IHorsePluginBuilder builder)
    {
        HorsePlugin plugin = builder.Build();

        if (Plugins.Any(x => string.Equals(x.Name, plugin.Name, StringComparison.InvariantCultureIgnoreCase)))
            throw new DuplicateNameException($"There is already active plugin with name: {plugin.Name}. Please remove it first.");

        plugin.Set(this);
        await plugin.Initialize();

        plugin.Initialized = true;
        plugin.Removed = false;

        var plugins = Plugins.ToList();
        plugins.Add(plugin);
        Plugins = plugins.ToArray();

        PluginData data = new PluginData();
        data.Name = plugin.Name;
        data.Disabled = false;

        lock (_data)
            assemblyData.Plugins.Add(data);
    }

    /// <summary>
    /// Removes plugin by name
    /// </summary>
    public async Task<bool> DisablePlugin(string pluginName, bool remove)
    {
        HorsePlugin plugin = Plugins.FirstOrDefault(x => string.Equals(x.Name, pluginName));
        if (plugin == null)
            return false;

        bool canRemove = await plugin.Remove();
        if (!canRemove)
            return false;

        plugin.Initialized = false;
        plugin.Removed = true;

        var list = Plugins.ToList();
        list.Remove(plugin);
        Plugins = list.ToArray();

        bool modified = false;
        lock (_data)
        {
            foreach (PluginAssemblyData data in _data)
            {
                PluginData pluginData = data.Plugins.FirstOrDefault(x => string.Equals(x.Name, pluginName, StringComparison.InvariantCultureIgnoreCase));
                if (pluginData != null)
                {
                    modified = true;
                    pluginData.Disabled = true;
                    pluginData.Removed = remove;

                    break;
                }
            }
        }

        if (modified)
            SaveData();

        return true;
    }

    /// <summary>
    /// Enabled, previously added and disabled plugin
    /// </summary>
    public async Task<bool> EnablePlugin(string pluginName)
    {
        PluginAssemblyData assemblyData = null;
        PluginData pdata = null;

        lock (_data)
        {
            foreach (PluginAssemblyData data in _data)
            {
                foreach (PluginData pluginData in data.Plugins)
                {
                    if (pluginData.Name.Equals(pluginName))
                    {
                        assemblyData = data;
                        pdata = pluginData;
                        break;
                    }
                }

                if (pdata != null)
                    break;
            }
        }

        if (pdata == null)
            return false;

        HorsePlugin plugin = Plugins.FirstOrDefault(x => x.Name.Equals(pluginName, StringComparison.InvariantCultureIgnoreCase));

        bool addAfterInit = false;
        if (plugin == null)
        {
            if (assemblyData == null)
                return false;

            if (assemblyData.LoadedAssembly == null)
                assemblyData.LoadedAssembly = Assembly.LoadFrom(assemblyData.Location);

            foreach (Type type in GetPluginBuilderTypesOfAssembly(assemblyData.LoadedAssembly))
            {
                IHorsePluginBuilder builder = (IHorsePluginBuilder) Activator.CreateInstance(type);
                string builderPluginName = builder.GetName();

                if (string.Equals(builderPluginName, pluginName))
                {
                    plugin = builder.Build();
                    plugin.Set(this);
                    addAfterInit = true;
                    break;
                }
            }
        }

        await plugin.Initialize();

        pdata.Removed = false;
        pdata.Disabled = false;

        if (addAfterInit)
        {
            var list = Plugins.ToList();
            list.Add(plugin);
            Plugins = list.ToArray();
        }

        return true;
    }

    /// <summary>
    /// Triggers active plugins
    /// </summary>
    internal void TriggerPluginHandlers(HorsePluginEvent sourceEvent, string targetName, HorseMessage message)
    {
        if (Plugins == null || Plugins.Length == 0)
            return;

        if (string.IsNullOrEmpty(targetName))
            return;

        string target = targetName;
        if (!targetName.StartsWith('@'))
        {
            switch (sourceEvent)
            {
                case HorsePluginEvent.ChannelPublish:
                    target = $"@channel:{targetName}";
                    break;

                case HorsePluginEvent.PluginMessage:
                    target = $"@plugin:{targetName}";
                    break;

                case HorsePluginEvent.QueuePush:
                    target = $"@queue:{targetName}";
                    break;

                case HorsePluginEvent.RouterPublish:
                    target = $"@router:{targetName}";
                    break;
            }
        }

        foreach (HorsePlugin plugin in Plugins)
        {
            if (target.StartsWith("@plugin:") && !string.Equals(plugin.Name, target.Substring(8)))
                continue;

            if (plugin.Handlers.Count == 0)
                continue;

            bool found = plugin.Handlers.TryGetValue(target, out var handler);

            if (!found)
                continue;

            _ = handler.Execute(new HorsePluginContext(sourceEvent, plugin, null, message));
        }
    }

    private IEnumerable<Type> GetPluginBuilderTypesOfAssembly(Assembly assembly)
    {
        foreach (Type type in assembly.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            if (typeof(IHorsePluginBuilder).IsAssignableFrom(type))
                yield return type;
        }
    }

    public async Task<bool> SendMessage(HorseMessage message)
    {
        switch (message.Type)
        {
            case MessageType.Channel:

                HorseChannel channel = _rider.Channel.Find(message.Target);
                if (channel == null)
                    return false;

                PushResult channelResult = channel.Push(message);
                return channelResult == PushResult.Success;

            case MessageType.Response:
                MessagingClient responseClient = _rider.Client.Find(message.Target);
                if (responseClient == null)
                    return false;

                await responseClient.SendAsync(message);
                return true;

            case MessageType.Router:

                Router router = _rider.Router.Find(message.Target);
                if (router == null)
                    return false;

                RouterPublishResult result = await router.Publish(null, message);
                return result == RouterPublishResult.OkWillNotRespond || result == RouterPublishResult.OkAndWillBeRespond;

            case MessageType.DirectMessage:

                if (message.Target.StartsWith("@type:", StringComparison.InvariantCultureIgnoreCase))
                {
                    List<MessagingClient> receivers = _rider.Client.FindByType(message.Target.Substring(6));
                    if (receivers.Count == 0)
                        return false;

                    if (message.HighPriority)
                    {
                        await receivers[0].SendAsync(message);
                        return true;
                    }

                    foreach (MessagingClient receiver in receivers)
                        _ = receiver.SendAsync(message);

                    return true;
                }

                if (message.Target.StartsWith("@name:", StringComparison.InvariantCultureIgnoreCase))
                {
                    List<MessagingClient> receivers = _rider.Client.FindClientByName(message.Target.Substring(6));
                    if (receivers.Count == 0)
                        return false;

                    if (message.HighPriority)
                    {
                        await receivers[0].SendAsync(message);
                        return true;
                    }

                    foreach (MessagingClient receiver in receivers)
                        _ = receiver.SendAsync(message);

                    return true;
                }

                MessagingClient directClient = _rider.Client.Find(message.Target);
                if (directClient == null)
                    return false;

                await directClient.SendAsync(message);
                return true;

            case MessageType.QueueMessage:

                HorseQueue queue = _rider.Queue.Find(message.Target);

                if (queue == null && _rider.Queue.Options.AutoQueueCreation)
                    queue = await _rider.Queue.Create(message.Target);

                if (queue == null)
                    return false;

                PushResult queueResult = await queue.Push(message);
                return queueResult == PushResult.Success;
        }

        return false;
    }
}