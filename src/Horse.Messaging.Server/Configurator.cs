using System.IO;
using System.Threading.Tasks;

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
            
            //wait for windows os (in next method we will read file from that directory and windows os sometimes throws directory does not exists exception)
            Task.Delay(1000).GetAwaiter().GetResult();
        }

        return configurationFolder;
    }

    public static T LoadConfigurationFromJson<T>(string jsonData) where T : class, new()
    {
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonData);
    }

    public static T LoadConfiguration<T>(string fullpath) where T : class, new()
    {
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
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(configuration);
        File.WriteAllText(fullpath, json);
    }
}