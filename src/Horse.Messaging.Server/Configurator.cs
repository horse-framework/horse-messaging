using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server;

internal class Configurator
{
    public static string Initialize(string configurationFolder)
    {
        if (string.IsNullOrEmpty(configurationFolder))
            configurationFolder = "data";

        if (configurationFolder.EndsWith("/") || configurationFolder.EndsWith("\\"))
            configurationFolder = configurationFolder[..^1];

        if (!Directory.Exists(configurationFolder))
        {
            Directory.CreateDirectory(configurationFolder);

            //windows os sometimes reports a just created directory as missing (next methods read files from it).
            //wait until it is visible, at most one second. It is visible immediately on most systems.
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!Directory.Exists(configurationFolder) && stopwatch.ElapsedMilliseconds < 1000)
                Thread.Sleep(10);
        }

        return configurationFolder;
    }

    public static T LoadConfigurationFromJson<T>(string jsonData) where T : class, new()
    {
        return JsonSerializer.Deserialize<T>(jsonData, SerializerFactory.Default());
    }

    public static T LoadConfiguration<T>(string fullpath) where T : class, new()
    {
        if (fullpath.StartsWith("..") || fullpath.Contains("..\\") || fullpath.Contains("../"))
            throw new FileLoadException("Configuration file path cannot contain '..' or '../' characters");
        
        if (!File.Exists(fullpath))
        {
            T config = new T();
            SaveConfiguration(fullpath, config);
            return config;
        }

        string json = File.ReadAllText(fullpath);
        return LoadConfigurationFromJson<T>(json);
    }

    public static void SaveConfiguration<T>(string fullpath, T configuration) where T : class, new()
    {
        string json = JsonSerializer.Serialize(configuration, SerializerFactory.Default());
        File.WriteAllText(fullpath, json);
    }
}