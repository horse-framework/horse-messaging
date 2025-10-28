using System.Reflection;
using System.Runtime.Loader;

namespace Horse.Messaging.Server.Plugins;

internal class PluginAssemblyLoadContext() : AssemblyLoadContext(isCollectible: true)
{
    protected override Assembly Load(AssemblyName assemblyName)
    {
        return null;
    }
}