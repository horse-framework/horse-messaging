using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Horse.Messaging.Client;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Routing;

public class RouterOptionsConfigurator : IOptionsConfigurator<RouterConfiguration>
{
    private readonly HorseRider _rider;
    private readonly string _filename;

    private List<RouterConfiguration> _configurations = new List<RouterConfiguration>();

    public RouterOptionsConfigurator(HorseRider rider, string filename)
    {
        _rider = rider;
        _filename = filename;
    }

    public RouterConfiguration[] Load()
    {
        string fullname = $"{_rider.Options.DataPath}/{_filename}";

        if (!File.Exists(fullname))
        {
            File.WriteAllText(fullname, "[]");

            lock (_configurations)
            {
                _configurations = new List<RouterConfiguration>();
                return _configurations.ToArray();
            }
        }

        string json = File.ReadAllText(fullname);

        lock (_configurations)
        {
            _configurations = JsonSerializer.Deserialize<List<RouterConfiguration>>(json, SerializerFactory.Default());
            return _configurations.ToArray();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(_configurations, SerializerFactory.Default());
        File.WriteAllText($"{_rider.Options.DataPath}/{_filename}", json);
    }

    public void Add(RouterConfiguration item)
    {
        lock (_configurations)
            _configurations.Add(item);
    }

    public RouterConfiguration Find(Func<RouterConfiguration, bool> predicate)
    {
        lock (_configurations)
        {
            RouterConfiguration configuration = _configurations.FirstOrDefault(predicate);
            return configuration;
        }
    }

    public void Remove(Func<RouterConfiguration, bool> predicate)
    {
        lock (_configurations)
        {
            RouterConfiguration configuration = _configurations.FirstOrDefault(predicate);
            if (configuration != null)
                _configurations.Remove(configuration);
        }
    }

    public void Remove(RouterConfiguration item)
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