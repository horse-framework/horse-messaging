using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Network;

/// <summary>
/// Messaging Queue message router implementation by message type
/// </summary>
public interface INetworkMessageHandler
{
    /// <summary>
    /// Handles the received message
    /// </summary>
    Task Handle(MessagingClient client, HorseMessage message, bool fromNode);
}