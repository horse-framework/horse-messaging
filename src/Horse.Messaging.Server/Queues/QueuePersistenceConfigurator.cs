using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Horse.Messaging.Server.Queues;

public class QueuePersistenceConfigurator : IPersistenceConfigurator<QueueConfiguration>
{
    private readonly string _path;
    private readonly string _filename;

    private List<QueueConfiguration> _configurations = new List<QueueConfiguration>();

    public QueuePersistenceConfigurator(string path, string filename)
    {
        if (!path.EndsWith('\\') && !path.EndsWith('/'))
            path += "/";

        _path = path;
        _filename = filename;
    }

    public QueueConfiguration[] Load()
    {
        if (!Directory.Exists(_path))
            Directory.CreateDirectory(_path);

        string fullname = $"{_path}{_filename}";

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
            _configurations = System.Text.Json.JsonSerializer.Deserialize<List<QueueConfiguration>>(json);
            return _configurations.ToArray();
        }
    }

    public void Save()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(_configurations);
        File.WriteAllText($"{_path}{_filename}", json);
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