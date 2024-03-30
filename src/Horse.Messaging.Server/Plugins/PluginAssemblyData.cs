using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Horse.Messaging.Server.Plugins;

/// <summary>
/// Persistent information of attached plugin assemblies
/// </summary>
public class PluginAssemblyData
{
    /// <summary>
    /// Assembly full name
    /// </summary>
    public string Fullname { get; set; }

    /// <summary>
    /// Assembly filename and location
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Assembly file version
    /// </summary>
    public string AssemblyVersion { get; set; }

    /// <summary>
    /// Included plugins in the assembly
    /// </summary>
    public List<PluginData> Plugins { get; set; }

    [JsonIgnore]
    internal Assembly LoadedAssembly { get; set; }
}