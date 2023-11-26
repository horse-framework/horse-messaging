using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Security;

/// <summary>
/// Checks if a client can subscribe to the queue.
/// </summary>
public interface IQueueAuthenticator
{
    /// <summary>
    /// Checks if a client can subscribe to the queue or get information about the queue.
    /// It should return true if allowed.
    /// </summary>
    Task<bool> Authenticate(HorseQueue queue, MessagingClient client);
}