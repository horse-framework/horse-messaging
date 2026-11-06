using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Channels;

public class ChannelOptionsConfigurator : IOptionsConfigurator<ChannelConfiguration>
{
    private readonly HorseRider _rider;
    private readonly string _filename;

    /// <summary>
    /// Guards the configuration list and the file.
    /// A dedicated object, the list instance is replaced by Load, locking on it would lock different objects.
    /// </summary>
    private readonly object _lock = new();

    private List<ChannelConfiguration> _configurations = new();

    public ChannelOptionsConfigurator(HorseRider rider, string filename)
    {
        _rider = rider;
        _filename = filename;
    }

    private string FullName => $"{_rider.Options.DataPath}/{_filename}";

    public ChannelConfiguration[] Load()
    {
        lock (_lock)
        {
            string fullname = FullName;

            if (!File.Exists(fullname))
            {
                File.WriteAllText(fullname, "[]");
                _configurations = new List<ChannelConfiguration>();
                return [];
            }

            string json = File.ReadAllText(fullname);
            _configurations = JsonSerializer.Deserialize<List<ChannelConfiguration>>(json, SerializerFactory.Default()) ?? new List<ChannelConfiguration>();
            return _configurations.ToArray();
        }
    }

    public void Save()
    {
        //serializing and writing under the same lock: the list cannot change while it is serialized
        //and concurrent saves cannot write the file at the same time
        lock (_lock)
        {
            string json = JsonSerializer.Serialize(_configurations, SerializerFactory.Default());
            File.WriteAllText(FullName, json);
        }
    }

    public void Add(ChannelConfiguration item)
    {
        lock (_lock)
            _configurations.Add(item);
    }

    public ChannelConfiguration Find(Func<ChannelConfiguration, bool> predicate)
    {
        lock (_lock)
            return _configurations.FirstOrDefault(predicate);
    }

    public void Remove(Func<ChannelConfiguration, bool> predicate)
    {
        lock (_lock)
        {
            ChannelConfiguration configuration = _configurations.FirstOrDefault(predicate);
            if (configuration != null)
                _configurations.Remove(configuration);
        }
    }

    public void Remove(ChannelConfiguration item)
    {
        lock (_lock)
            _configurations.Remove(item);
    }

    public void Clear()
    {
        lock (_lock)
            _configurations.Clear();
    }
}
