using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Security;

/// <summary>
/// Checks if a client can connect to the server
/// </summary>
public interface IClientAuthenticator
{
    /// <summary>
    /// Checks if a client can connect to the server
    /// It should return true if allowed.
    /// </summary>
    Task<bool> Authenticate(HorseRider server, MessagingClient client);
}