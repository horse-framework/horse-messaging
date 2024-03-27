using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Horse.Messaging.Server.Plugins;

internal class PluginData
{
    public string Name { get; set; }
    public string FullTypeName { get; set; }
    public bool Disabled { get; set; }
    public bool Removed { get; set; }
}

internal class PluginAssemblyData
{
    public string Fullname { get; set; }
    public string Location { get; set; }
    public string Filename { get; set; }
    public string AssemblyVersion { get; set; }

    public List<PluginData> Plugins { get; set; }

    [JsonIgnore]
    internal Assembly LoadedAssembly { get; set; }
}