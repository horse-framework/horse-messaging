using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Plugins;

public interface IPluginRider
{
    /// <summary>
    /// Server port could be used if plugin needs some client operations, it can connect to the server itself with using a horse client
    /// </summary>
    public int ServerPort { get; }

    /// <summary>
    /// Sends a message from plugin to server or client 
    /// </summary>
    public Task<bool> SendMessage(HorseMessage message);
}