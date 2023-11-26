using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Queues;

public class QueueOptionsConfigurator : IOptionsConfigurator<QueueConfiguration>
{
    private readonly HorseRider _rider;
    private readonly string _filename;

    private List<QueueConfiguration> _configurations = new();

    public QueueOptionsConfigurator(HorseRider rider, string filename)
    {
        _rider = rider;
        _filename = filename;
    }

    public QueueConfiguration[] Load()
    {
        string fullname = $"{_rider.Options.DataPath}/{_filename}";

        if (!File.Exists(fullname))
        {
            File.WriteAllText(fullname, "[]");

            lock (_configurations)
            {
                _configurations = new List<QueueConfiguration>();
                return _configurations.ToArray();
            }
        }

        string json = File.ReadAllText(fullname);

        lock (_configurations)
        {
            _configurations = JsonSerializer.Deserialize<List<QueueConfiguration>>(json, SerializerFactory.Default());
            return _configurations.ToArray();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(_configurations, SerializerFactory.Default());
        File.WriteAllText($"{_rider.Options.DataPath}/{_filename}", json);
    }

    public void Add(QueueConfiguration item)
    {
        lock (_configurations)
            _configurations.Add(item);
    }

    public QueueConfiguration Find(Func<QueueConfiguration, bool> predicate)
    {
        lock (_configurations)
        {
            QueueConfiguration configuration = _configurations.FirstOrDefault(predicate);
            return configuration;
        }
    }

    public void Remove(Func<QueueConfiguration, bool> predicate)
    {
        lock (_configurations)
        {
            QueueConfiguration configuration = _configurations.FirstOrDefault(predicate);
            if (configuration != null)
                _configurations.Remove(configuration);
        }
    }

    public void Remove(QueueConfiguration item)
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