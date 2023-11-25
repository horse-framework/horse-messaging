using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server;

/// <summary>
/// Implementation for messaging between client and server
/// </summary>
public interface IServerMessageHandler
{
    /// <summary>
    /// when a client sends a message to server
    /// </summary>
    Task Received(MessagingClient client, HorseMessage message);
}