using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// Saves router configurations into a json file in the data path.
/// Thread safe: routers and bindings can be changed by many clients at the same time.
/// </summary>
public class RouterOptionsConfigurator : IOptionsConfigurator<RouterConfiguration>
{
    private readonly HorseRider _rider;
    private readonly string _filename;

    /// <summary>
    /// Guards the list and the file. The list field is replaced by Load, so it is not used as the lock.
    /// </summary>
    private readonly object _sync = new();

    private List<RouterConfiguration> _configurations = new();

    public RouterOptionsConfigurator(HorseRider rider, string filename)
    {
        _rider = rider;
        _filename = filename;
    }

    private string FullName => $"{_rider.Options.DataPath}/{_filename}";

    public RouterConfiguration[] Load()
    {
        lock (_sync)
        {
            string fullname = FullName;

            if (!File.Exists(fullname))
            {
                File.WriteAllText(fullname, "[]");
                _configurations = new List<RouterConfiguration>();
                return Array.Empty<RouterConfiguration>();
            }

            string json = File.ReadAllText(fullname);
            _configurations = JsonSerializer.Deserialize<List<RouterConfiguration>>(json, SerializerFactory.Default()) ?? new List<RouterConfiguration>();
            return _configurations.ToArray();
        }
    }

    /// <summary>
    /// Writes all configurations into a temporary file and moves it over the data file.
    /// A crash while writing leaves the previous file as it was, instead of a partly written one
    /// that the server cannot load at its next start.
    /// </summary>
    public void Save()
    {
        lock (_sync)
        {
            string json = JsonSerializer.Serialize(_configurations, SerializerFactory.Default());
            string fullname = FullName;
            string temporary = fullname + ".tmp";

            File.WriteAllText(temporary, json);
            File.Move(temporary, fullname, true);
        }
    }

    public void Add(RouterConfiguration item)
    {
        lock (_sync)
            _configurations.Add(item);
    }

    public RouterConfiguration Find(Func<RouterConfiguration, bool> predicate)
    {
        lock (_sync)
            return _configurations.FirstOrDefault(predicate);
    }

    public void Remove(Func<RouterConfiguration, bool> predicate)
    {
        lock (_sync)
        {
            RouterConfiguration configuration = _configurations.FirstOrDefault(predicate);
            if (configuration != null)
                _configurations.Remove(configuration);
        }
    }

    public void Remove(RouterConfiguration item)
    {
        lock (_sync)
            _configurations.Remove(item);
    }

    public void Clear()
    {
        lock (_sync)
            _configurations.Clear();
    }
}
