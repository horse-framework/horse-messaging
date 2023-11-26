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

    private List<ChannelConfiguration> _configurations = new();

    public ChannelOptionsConfigurator(HorseRider rider, string filename)
    {
        _rider = rider;
        _filename = filename;
    }

    public ChannelConfiguration[] Load()
    {
        string fullname = $"{_rider.Options.DataPath}/{_filename}";

        if (!File.Exists(fullname))
        {
            File.WriteAllText(fullname, "[]");

            lock (_configurations)
            {
                _configurations = new List<ChannelConfiguration>();
                return _configurations.ToArray();
            }
        }

        string json = File.ReadAllText(fullname);

        lock (_configurations)
        {
            _configurations = JsonSerializer.Deserialize<List<ChannelConfiguration>>(json, SerializerFactory.Default());
            return _configurations.ToArray();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(_configurations, SerializerFactory.Default());
        File.WriteAllText($"{_rider.Options.DataPath}/{_filename}", json);
    }

    public void Add(ChannelConfiguration item)
    {
        lock (_configurations)
            _configurations.Add(item);
    }

    public ChannelConfiguration Find(Func<ChannelConfiguration, bool> predicate)
    {
        lock (_configurations)
        {
            ChannelConfiguration configuration = _configurations.FirstOrDefault(predicate);
            return configuration;
        }
    }

    public void Remove(Func<ChannelConfiguration, bool> predicate)
    {
        lock (_configurations)
        {
            ChannelConfiguration configuration = _configurations.FirstOrDefault(predicate);
            if (configuration != null)
                _configurations.Remove(configuration);
        }
    }

    public void Remove(ChannelConfiguration item)
    {
        lock (_configurations)
            _configurations.Remove(item);
    }

    public void Clear()
    {
        lock (_configurations)
            _configurations.Clear();
    }
}