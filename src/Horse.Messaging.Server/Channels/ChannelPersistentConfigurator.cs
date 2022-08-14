using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Horse.Messaging.Server.Channels;

public class ChannelPersistentConfigurator : IPersistenceConfigurator<ChannelConfiguration>
{
    private readonly string _path;
    private readonly string _filename;

    private List<ChannelConfiguration> _configurations = new List<ChannelConfiguration>();

    public ChannelPersistentConfigurator(string path, string filename)
    {
        if (!path.EndsWith('\\') && !path.EndsWith('/'))
            path += "/";

        _path = path;
        _filename = filename;
    }

    public ChannelConfiguration[] Load()
    {
        if (!Directory.Exists(_path))
            Directory.CreateDirectory(_path);

        string fullname = $"{_path}{_filename}";

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
            _configurations = System.Text.Json.JsonSerializer.Deserialize<List<ChannelConfiguration>>(json);
            return _configurations.ToArray();
        }
    }

    public void Save()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(_configurations);
        File.WriteAllText($"{_path}{_filename}", json);
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