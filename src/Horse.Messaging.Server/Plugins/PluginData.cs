namespace Horse.Messaging.Server.Plugins;

/// <summary>
/// Persistent plugin data
/// </summary>
public class PluginData
{
    /// <summary>
    /// Plugin name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Builder type name of the plugin in assembly
    /// </summary>
    public string BuilderTypeName { get; set; }

    /// <summary>
    /// True, if the plugin is disabled
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// True, if the plugin is removed.
    /// Removed plugins will be removed from the data after server restart.
    /// </summary>
    public bool Removed { get; set; }
}